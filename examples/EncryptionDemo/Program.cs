// EncryptionDemo example application - demonstrates field encryption and sensitive data logging
// This example shows how to use [Encrypted] and [Sensitive] attributes with FluentDynamoDb

using Amazon.DynamoDBv2.Model;
using EncryptionDemo;
using EncryptionDemo.Entities;
using Examples.Shared;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Encryption.Kms;
using Oproto.FluentDynamoDb.Hydration;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Extensions;

// Alias for the generated table class
using SecureRecordsTable = EncryptionDemo.Entities.EncryptionDemoTable;

// Table name as external configuration - in real apps this would come from
// environment variables, configuration files, or other external sources
const string TableName = "encryption-demo";

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║       EncryptionDemo - FluentDynamoDb Example              ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: Field encryption with KMS and sensitive     ║");
Console.WriteLine("║  data redaction in logs                                    ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("  Provide a KMS Key ARN to enable [Encrypted] field encryption.");
Console.WriteLine("  AWS credentials are resolved from the environment (AWS_PROFILE, etc.).");
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine();

// Prompt for KMS key ARN
Console.Write("Enter KMS Key ARN (or press Enter to skip): ");
var kmsKeyArn = Console.ReadLine()?.Trim();

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var client = DynamoDbSetup.CreateLocalClient();

// Ensure table exists (idempotent)
ConsoleHelpers.ShowInfo("Ensuring table exists...");
var created = await DynamoDbSetup.EnsureTableExistsAsync(
    client,
    TableName,
    "pk");

if (created)
{
    ConsoleHelpers.ShowSuccess($"Created table '{TableName}'");
}
else
{
    ConsoleHelpers.ShowInfo($"Table '{TableName}' already exists");
}

// Register the source-generated hydrator for async encryption serialization
DefaultEntityHydratorRegistry.Instance.RegisterSecureRecordHydrator();

// Configure FluentDynamoDbOptions with logger and optional encryptor
var logger = new ConsoleLogger(LogLevel.Debug);
var optionsBuilder = new FluentDynamoDbOptions()
    .WithLogger(logger);

// Configure encryption if KMS key was provided
var encryptionConfigured = false;
if (!string.IsNullOrWhiteSpace(kmsKeyArn))
{
    try
    {
        var keyResolver = new DefaultKmsKeyResolver(kmsKeyArn);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        optionsBuilder = optionsBuilder.WithEncryption(encryptor);
        encryptionConfigured = true;
        ConsoleHelpers.ShowSuccess($"Configured encryption with KMS key: {kmsKeyArn[..Math.Min(20, kmsKeyArn.Length)]}...");
    }
    catch (Exception ex)
    {
        ConsoleHelpers.ShowWarning($"Could not configure encryption: {ex.Message}");
        ConsoleHelpers.ShowInfo("Continuing without encryption...");
    }
}
else
{
    ConsoleHelpers.ShowInfo("No KMS key provided - encryption features will not be available");
}

var options = optionsBuilder;
var table = new SecureRecordsTable(client, TableName, options);

Console.WriteLine();
ConsoleHelpers.ShowInfo("Console logger is active - watch for [REDACTED] in log output!");
Console.WriteLine();

// Main menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu(
        "Encryption Demo Menu",
        "Create Secure Record",
        "List All Records",
        "View Record Details",
        "Delete Record",
        "Show Logging Demo",
        "Round-Trip Encryption Demo",
        "Exit");

    try
    {
        switch (choice)
        {
            case 1:
                await CreateSecureRecordAsync(table, options);
                break;
            case 2:
                await ListRecordsAsync(table);
                break;
            case 3:
                await ViewRecordDetailsAsync(table);
                break;
            case 4:
                await DeleteRecordAsync(table);
                break;
            case 5:
                ShowLoggingDemo(logger);
                break;
            case 6:
                await RunRoundTripDemoAsync(table, client, encryptionConfigured);
                break;
            case 7:
                ConsoleHelpers.ShowInfo("Goodbye!");
                return;
            case 0:
                // Invalid selection - menu already showed error
                break;
        }
    }
    catch (NotImplementedException ex)
    {
        ConsoleHelpers.ShowWarning($"Feature not yet implemented: {ex.Message}");
    }
    catch (Exception ex)
    {
        ConsoleHelpers.ShowError(ex, "Operation failed");
    }
}


/// <summary>
/// Creates a new secure record with sample sensitive data.
/// Demonstrates [Sensitive] attribute redaction in logs.
/// </summary>
static async Task CreateSecureRecordAsync(SecureRecordsTable table, FluentDynamoDbOptions options)
{
    ConsoleHelpers.ShowSection("Create New Secure Record");
    
    var label = ConsoleHelpers.GetInput("Enter a label for this record");
    if (string.IsNullOrWhiteSpace(label))
        return;

    var email = ConsoleHelpers.GetInput("Enter email address (will be marked [Sensitive])");
    if (string.IsNullOrWhiteSpace(email))
        return;

    Console.Write("Enter SSN (will be [Encrypted], or press Enter to skip): ");
    var ssn = Console.ReadLine()?.Trim() ?? string.Empty;

    Console.Write("Enter credit card number (will be [Encrypted] + [Sensitive], or press Enter to skip): ");
    var creditCard = Console.ReadLine()?.Trim() ?? string.Empty;

    var record = new SecureRecord
    {
        Id = Guid.NewGuid().ToString(),
        Label = label,
        Email = email,
        SocialSecurityNumber = ssn,
        CreditCardNumber = creditCard,
        CreatedAt = DateTime.UtcNow
    };

    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Storing record - watch the log output below:");
    Console.WriteLine("─".PadRight(60, '─'));

    // Store the record - sensitive fields will be redacted in logs
    // Encrypted fields are encrypted via the async serialization path
    await table.Put<SecureRecord>().WithItem(record).PutAsync();
    
    Console.WriteLine("─".PadRight(60, '─'));
    ConsoleHelpers.ShowSuccess($"Created record with ID: {record.Id[..8]}...");
    
    Console.WriteLine();
    Console.WriteLine("  Notice in the logs above:");
    Console.WriteLine("  - The 'email' field shows [REDACTED] (marked with [Sensitive])");
    Console.WriteLine("  - The 'creditCard' field shows [REDACTED] (marked with [Sensitive])");
    Console.WriteLine("  - The 'label' field shows the actual value (not sensitive)");
    Console.WriteLine();
    Console.WriteLine("  In DynamoDB, when encryption is configured:");
    Console.WriteLine($"  - Email: {email} (stored as plaintext - not encrypted)");
    Console.WriteLine($"  - SSN: {(string.IsNullOrEmpty(ssn) ? "(empty)" : "encrypted as binary blob in DynamoDB")}");
    Console.WriteLine($"  - Credit Card: {(string.IsNullOrEmpty(creditCard) ? "(empty)" : "encrypted as binary blob in DynamoDB")}");
}

/// <summary>
/// Lists all secure records in the table.
/// </summary>
static async Task ListRecordsAsync(SecureRecordsTable table)
{
    ConsoleHelpers.ShowSection("All Secure Records");
    
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Scanning records - watch for [REDACTED] in log output:");
    Console.WriteLine("─".PadRight(60, '─'));
    
    var records = await table.SecureRecords.Scan().ToListAsync();
    
    Console.WriteLine("─".PadRight(60, '─'));
    
    var recordList = records.ToList();
    
    if (recordList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No records found. Create some!");
        return;
    }

    ConsoleHelpers.DisplayTable(
        recordList,
        ("ID (first 8)", r => r.Id[..Math.Min(8, r.Id.Length)]),
        ("Label", r => TruncateString(r.Label, 20)),
        ("Email", r => TruncateString(r.Email, 25)),
        ("Created", r => r.CreatedAt.ToString("yyyy-MM-dd HH:mm")));

    ConsoleHelpers.ShowInfo($"Total: {recordList.Count} records");
}

/// <summary>
/// Views detailed information about a specific record.
/// Demonstrates that actual values are stored in DynamoDB despite log redaction.
/// </summary>
static async Task ViewRecordDetailsAsync(SecureRecordsTable table)
{
    ConsoleHelpers.ShowSection("View Record Details");
    
    var recordList = await table.SecureRecords.Scan().ToListAsync();
    
    if (recordList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No records to view.");
        return;
    }

    Console.WriteLine("Available records:");
    ConsoleHelpers.DisplayTable(
        recordList,
        ("ID (first 8)", r => r.Id[..Math.Min(8, r.Id.Length)]),
        ("Label", r => TruncateString(r.Label, 40)));

    var id = ConsoleHelpers.GetInput("Enter record ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    var record = recordList.FirstOrDefault(r => r.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (record == null)
    {
        ConsoleHelpers.ShowError($"No record found matching '{id}'");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("  Record Details (actual values from DynamoDB):");
    Console.WriteLine($"  ─────────────────────────────────────────────");
    Console.WriteLine($"  ID:          {record.Id}");
    Console.WriteLine($"  Label:       {record.Label}");
    Console.WriteLine($"  Email:       {record.Email}");
    Console.WriteLine($"  SSN:         {(string.IsNullOrEmpty(record.SocialSecurityNumber) ? "(empty)" : MaskSsn(record.SocialSecurityNumber))}");
    Console.WriteLine($"  Credit Card: {(string.IsNullOrEmpty(record.CreditCardNumber) ? "(empty)" : MaskCreditCard(record.CreditCardNumber))}");
    Console.WriteLine($"  Created:     {record.CreatedAt:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine();
    Console.WriteLine("  Note: Sensitive values are shown masked here for display purposes,");
    Console.WriteLine("  but the full values are stored in DynamoDB. The [Sensitive] attribute");
    Console.WriteLine("  only affects log output, not storage.");
}

/// <summary>
/// Deletes a record from the table.
/// </summary>
static async Task DeleteRecordAsync(SecureRecordsTable table)
{
    ConsoleHelpers.ShowSection("Delete Record");
    
    var recordList = await table.SecureRecords.Scan().ToListAsync();
    
    if (recordList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No records to delete.");
        return;
    }

    Console.WriteLine("Available records:");
    ConsoleHelpers.DisplayTable(
        recordList,
        ("ID (first 8)", r => r.Id[..Math.Min(8, r.Id.Length)]),
        ("Label", r => TruncateString(r.Label, 40)));

    var id = ConsoleHelpers.GetInput("Enter record ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    var record = recordList.FirstOrDefault(r => r.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (record == null)
    {
        ConsoleHelpers.ShowError($"No record found matching '{id}'");
        return;
    }

    Console.Write($"Are you sure you want to delete '{record.Label}'? (y/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "y" && confirm != "yes")
    {
        ConsoleHelpers.ShowInfo("Delete cancelled");
        return;
    }

    await table.SecureRecords.DeleteAsync(record.Id);
    ConsoleHelpers.ShowSuccess("Record deleted successfully");
}

/// <summary>
/// Demonstrates logging behavior with sensitive data redaction.
/// </summary>
static void ShowLoggingDemo(ConsoleLogger logger)
{
    ConsoleHelpers.ShowSection("Logging Demo");
    
    Console.WriteLine();
    Console.WriteLine("  This demo shows how the ConsoleLogger displays different log levels:");
    Console.WriteLine();
    
    logger.LogTrace(0, "This is a TRACE message - most verbose level");
    logger.LogDebug(0, "This is a DEBUG message - for development");
    logger.LogInformation(0, "This is an INFO message - general flow");
    logger.LogWarning(0, "This is a WARNING message - something unexpected");
    logger.LogError(0, "This is an ERROR message - operation failed");
    
    Console.WriteLine();
    Console.WriteLine("  Key points about sensitive data logging:");
    Console.WriteLine("  ─────────────────────────────────────────");
    Console.WriteLine("  • [Sensitive] attribute: Values show as [REDACTED] in logs");
    Console.WriteLine("  • [Encrypted] attribute: Values are encrypted before storage");
    Console.WriteLine("  • Both can be combined for maximum protection");
    Console.WriteLine("  • Actual values are always stored in DynamoDB");
    Console.WriteLine("  • Log redaction prevents accidental exposure in log files");
    Console.WriteLine();
}

/// <summary>
/// Truncates a string to the specified maximum length.
/// </summary>
static string TruncateString(string value, int maxLength)
{
    if (string.IsNullOrEmpty(value))
        return string.Empty;
    
    return value.Length <= maxLength 
        ? value 
        : value[..(maxLength - 3)] + "...";
}

/// <summary>
/// Masks an SSN for display (shows last 4 digits).
/// </summary>
static string MaskSsn(string ssn)
{
    if (ssn.Length <= 4)
        return "***-**-" + ssn;
    return "***-**-" + ssn[^4..];
}

/// <summary>
/// Masks a credit card number for display (shows last 4 digits).
/// </summary>
static string MaskCreditCard(string cardNumber)
{
    var digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());
    if (digitsOnly.Length <= 4)
        return "****-****-****-" + digitsOnly;
    return "****-****-****-" + digitsOnly[^4..];
}

/// <summary>
/// Demonstrates a complete round-trip encryption flow: create, inspect raw encrypted data,
/// retrieve with automatic decryption, and clean up.
/// </summary>
static async Task RunRoundTripDemoAsync(SecureRecordsTable table, Amazon.DynamoDBv2.IAmazonDynamoDB client, bool encryptionConfigured)
{
    ConsoleHelpers.ShowSection("Round-Trip Encryption Demo");

    if (!encryptionConfigured)
    {
        ConsoleHelpers.ShowInfo("Encryption is not configured. Provide a KMS Key ARN at startup to use this demo.");
        return;
    }

    const string recordId = "demo-round-trip-001";

    var record = new SecureRecord
    {
        Id = recordId,
        Label = "Round-Trip Demo Record",
        Email = "demo@example.com",
        SocialSecurityNumber = "123-45-6789",
        CreditCardNumber = "4111-1111-1111-1111",
        CreatedAt = DateTime.UtcNow
    };

    // Step 1: Store the record with encryption
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Step 1: Storing record with encrypted fields...");
    Console.WriteLine("─".PadRight(60, '─'));
    await table.Put<SecureRecord>().WithItem(record).PutAsync();
    Console.WriteLine("─".PadRight(60, '─'));
    ConsoleHelpers.ShowSuccess("Record stored successfully.");

    // Step 2: Read raw attributes directly via the DynamoDB SDK
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Step 2: Reading raw attributes directly from DynamoDB...");
    var rawResponse = await client.GetItemAsync(new GetItemRequest
    {
        TableName = TableName,
        Key = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = recordId } }
        }
    });

    Console.WriteLine();
    Console.WriteLine("  Raw DynamoDB attributes:");
    Console.WriteLine("  ─────────────────────────────────────────────");

    foreach (var attr in rawResponse.Item)
    {
        if (attr.Value.B != null)
        {
            var base64 = Convert.ToBase64String(attr.Value.B.ToArray());
            Console.WriteLine($"  {attr.Key}: [Binary] {base64[..Math.Min(60, base64.Length)]}...");
        }
        else if (attr.Value.S != null)
        {
            Console.WriteLine($"  {attr.Key}: {attr.Value.S}");
        }
    }

    // Step 3: Retrieve via FluentDynamoDb pipeline (auto-decrypts)
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Step 3: Retrieving record via FluentDynamoDb (auto-decrypts)...");
    Console.WriteLine("─".PadRight(60, '─'));
    var decrypted = await table.SecureRecords.Get(recordId).GetItemAsync();
    Console.WriteLine("─".PadRight(60, '─'));

    if (decrypted == null)
    {
        ConsoleHelpers.ShowError("Failed to retrieve the demo record after storing it.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("  Decrypted record values:");
    Console.WriteLine("  ─────────────────────────────────────────────");
    Console.WriteLine($"  ID:          {decrypted.Id}");
    Console.WriteLine($"  Label:       {decrypted.Label}");
    Console.WriteLine($"  Email:       {decrypted.Email}");
    Console.WriteLine($"  SSN:         {decrypted.SocialSecurityNumber}");
    Console.WriteLine($"  Credit Card: {decrypted.CreditCardNumber}");
    Console.WriteLine($"  Created:     {decrypted.CreatedAt:yyyy-MM-dd HH:mm:ss}");

    // Step 4: Verify round-trip
    Console.WriteLine();
    var ssnMatch = decrypted.SocialSecurityNumber == record.SocialSecurityNumber;
    var ccMatch = decrypted.CreditCardNumber == record.CreditCardNumber;

    if (ssnMatch && ccMatch)
    {
        ConsoleHelpers.ShowSuccess("Round-trip verified: decrypted values match the originals.");
    }
    else
    {
        ConsoleHelpers.ShowWarning("Round-trip mismatch detected:");
        if (!ssnMatch) ConsoleHelpers.ShowWarning($"  SSN: expected '{record.SocialSecurityNumber}', got '{decrypted.SocialSecurityNumber}'");
        if (!ccMatch) ConsoleHelpers.ShowWarning($"  Credit Card: expected '{record.CreditCardNumber}', got '{decrypted.CreditCardNumber}'");
    }

    // Step 5: Clean up
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Step 5: Cleaning up demo record...");
    await table.SecureRecords.DeleteAsync(recordId);
    ConsoleHelpers.ShowSuccess("Demo record deleted.");
    Console.WriteLine();
}
