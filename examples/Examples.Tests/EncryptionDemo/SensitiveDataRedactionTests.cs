using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;

namespace Examples.Tests.EncryptionDemo;

/// <summary>
/// Property-based tests for sensitive data redaction in logs.
/// These tests verify that the [Sensitive] attribute correctly redacts values in log output
/// while preserving the actual values in DynamoDB storage.
/// </summary>
public class SensitiveDataRedactionTests
{
    private const string RedactedPlaceholder = "[REDACTED]";

    /// <summary>
    /// **Feature: v0.9.0-enhancements, Property 4: Sensitive Data Redaction in Logs**
    /// **Validates: Requirements 4.6, 4.7**
    /// 
    /// For any entity with Sensitive properties, log output should contain "[REDACTED]"
    /// for sensitive values while DynamoDB should contain the actual values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SensitiveFields_AreRedacted_InLogOutput()
    {
        return Prop.ForAll(
            GenerateSensitiveFieldData(),
            data =>
            {
                // Create a DynamoDB item with the sensitive field
                var item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = data.Id },
                    ["label"] = new AttributeValue { S = data.Label },
                    [data.SensitiveFieldName] = new AttributeValue { S = data.SensitiveValue }
                };

                // Define which fields are sensitive
                var sensitiveFields = new HashSet<string> { data.SensitiveFieldName };

                // Redact the item for logging
                var redactedItem = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

                // Verify the sensitive field is redacted in the log output
                var sensitiveFieldRedacted = redactedItem![data.SensitiveFieldName].S == RedactedPlaceholder;

                // Verify the original item still contains the actual value
                var originalPreserved = item[data.SensitiveFieldName].S == data.SensitiveValue;

                // Verify non-sensitive fields are not redacted
                var nonSensitivePreserved = redactedItem["label"].S == data.Label;

                return (sensitiveFieldRedacted && originalPreserved && nonSensitivePreserved)
                    .ToProperty()
                    .Label($"SensitiveRedacted: {sensitiveFieldRedacted}, " +
                           $"OriginalPreserved: {originalPreserved}, " +
                           $"NonSensitivePreserved: {nonSensitivePreserved}");
            });
    }

    /// <summary>
    /// **Feature: v0.9.0-enhancements, Property 4: Sensitive Data Redaction in Logs**
    /// **Validates: Requirements 4.6, 4.7**
    /// 
    /// For any entity with multiple Sensitive properties, all sensitive values should be
    /// redacted in log output while non-sensitive values remain visible.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleSensitiveFields_AllRedacted_InLogOutput()
    {
        return Prop.ForAll(
            GenerateMultipleSensitiveFieldData(),
            data =>
            {
                // Create a DynamoDB item with multiple sensitive fields
                var item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = data.Id },
                    ["label"] = new AttributeValue { S = data.Label },
                    ["email"] = new AttributeValue { S = data.Email },
                    ["ssn"] = new AttributeValue { S = data.Ssn },
                    ["creditCard"] = new AttributeValue { S = data.CreditCard }
                };

                // Define which fields are sensitive (email and creditCard per the SecureRecord entity)
                var sensitiveFields = new HashSet<string> { "email", "creditCard" };

                // Redact the item for logging
                var redactedItem = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

                // Verify all sensitive fields are redacted
                var emailRedacted = redactedItem!["email"].S == RedactedPlaceholder;
                var creditCardRedacted = redactedItem["creditCard"].S == RedactedPlaceholder;

                // Verify non-sensitive fields are preserved
                var labelPreserved = redactedItem["label"].S == data.Label;
                var ssnPreserved = redactedItem["ssn"].S == data.Ssn; // SSN is [Encrypted] but not [Sensitive]

                // Verify original values are preserved in the original item
                var originalEmailPreserved = item["email"].S == data.Email;
                var originalCreditCardPreserved = item["creditCard"].S == data.CreditCard;

                var allConditionsMet = emailRedacted && creditCardRedacted && 
                                       labelPreserved && ssnPreserved &&
                                       originalEmailPreserved && originalCreditCardPreserved;

                return allConditionsMet
                    .ToProperty()
                    .Label($"EmailRedacted: {emailRedacted}, " +
                           $"CreditCardRedacted: {creditCardRedacted}, " +
                           $"LabelPreserved: {labelPreserved}, " +
                           $"SsnPreserved: {ssnPreserved}");
            });
    }

    /// <summary>
    /// **Feature: v0.9.0-enhancements, Property 4: Sensitive Data Redaction in Logs**
    /// **Validates: Requirements 4.6, 4.7**
    /// 
    /// For any string value, RedactIfSensitive should return [REDACTED] if the field
    /// is in the sensitive fields set, otherwise return the original value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RedactIfSensitive_ReturnsCorrectValue_BasedOnFieldSensitivity()
    {
        return Prop.ForAll(
            GenerateRedactIfSensitiveData(),
            data =>
            {
                var sensitiveFields = new HashSet<string>(data.SensitiveFieldNames);

                var result = SensitiveDataRedactor.RedactIfSensitive(
                    data.Value, 
                    data.FieldName, 
                    sensitiveFields);

                var expectedResult = data.SensitiveFieldNames.Contains(data.FieldName)
                    ? RedactedPlaceholder
                    : data.Value;

                return (result == expectedResult)
                    .ToProperty()
                    .Label($"Field: {data.FieldName}, " +
                           $"IsSensitive: {data.SensitiveFieldNames.Contains(data.FieldName)}, " +
                           $"Result: {result}, Expected: {expectedResult}");
            });
    }

    #region Generators

    /// <summary>
    /// Generates test data for single sensitive field tests.
    /// </summary>
    private static Arbitrary<SensitiveFieldData> GenerateSensitiveFieldData()
    {
        var idGen = Gen.Elements("id-1", "id-2", "id-3", "id-4", "id-5");
        var labelGen = Gen.Elements("Record A", "Record B", "Test Record", "Sample", "Demo");
        var fieldNameGen = Gen.Elements("email", "ssn", "creditCard", "phoneNumber", "password");
        var valueGen = Gen.Elements(
            "user@example.com", 
            "123-45-6789", 
            "4111-1111-1111-1111",
            "+1-555-123-4567",
            "secretPassword123");

        return Arb.From(
            from id in idGen
            from label in labelGen
            from fieldName in fieldNameGen
            from value in valueGen
            select new SensitiveFieldData(id, label, fieldName, value));
    }

    /// <summary>
    /// Generates test data for multiple sensitive field tests.
    /// </summary>
    private static Arbitrary<MultipleSensitiveFieldData> GenerateMultipleSensitiveFieldData()
    {
        var idGen = Gen.Elements("id-1", "id-2", "id-3", "id-4", "id-5");
        var labelGen = Gen.Elements("Record A", "Record B", "Test Record", "Sample", "Demo");
        var emailGen = Gen.Elements(
            "alice@example.com", 
            "bob@test.org", 
            "charlie@demo.net",
            "user@company.io");
        var ssnGen = Gen.Elements(
            "123-45-6789", 
            "987-65-4321", 
            "555-12-3456",
            "111-22-3333");
        var creditCardGen = Gen.Elements(
            "4111-1111-1111-1111", 
            "5500-0000-0000-0004", 
            "3400-0000-0000-009",
            "6011-0000-0000-0004");

        return Arb.From(
            from id in idGen
            from label in labelGen
            from email in emailGen
            from ssn in ssnGen
            from creditCard in creditCardGen
            select new MultipleSensitiveFieldData(id, label, email, ssn, creditCard));
    }

    /// <summary>
    /// Generates test data for RedactIfSensitive tests.
    /// </summary>
    private static Arbitrary<RedactIfSensitiveData> GenerateRedactIfSensitiveData()
    {
        var fieldNameGen = Gen.Elements("email", "ssn", "creditCard", "label", "name", "id");
        var valueGen = Gen.Elements(
            "test@example.com", 
            "123-45-6789", 
            "4111-1111-1111-1111",
            "Test Label",
            "John Doe",
            "user-123");
        var sensitiveFieldsGen = Gen.SubListOf(new[] { "email", "creditCard", "ssn", "phoneNumber" })
            .Select(list => list.ToArray());

        return Arb.From(
            from fieldName in fieldNameGen
            from value in valueGen
            from sensitiveFields in sensitiveFieldsGen
            select new RedactIfSensitiveData(fieldName, value, sensitiveFields));
    }

    #endregion

    #region Test Data Records

    private record SensitiveFieldData(
        string Id, 
        string Label, 
        string SensitiveFieldName, 
        string SensitiveValue);

    private record MultipleSensitiveFieldData(
        string Id, 
        string Label, 
        string Email, 
        string Ssn, 
        string CreditCard);

    private record RedactIfSensitiveData(
        string FieldName, 
        string Value, 
        string[] SensitiveFieldNames);

    #endregion
}
