// EncryptionDemo example application - demonstrates field encryption and sensitive data logging
// This example shows how to use [Encrypted] and [Sensitive] attributes with FluentDynamoDb

using EncryptionDemo;
using EncryptionDemo.Entities;
using Examples.Shared;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Encryption.Kms;
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

// Display important warning about encryption status
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("⚠ IMPORTANT: AWS Encryption SDK integration is pending completion.");
Console.WriteLine("  This demo shows the intended API pattern and demonstrates:");
Console.WriteLine("  - [Sensitive] attribute for log redaction (fully working)");
Console.WriteLine("  - [Encrypted] attribute API (encryption not yet implemented)");
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.White;

// Prompt for KMS key ARN (optional for this demo since encryption isn't complete)
Console.Write("Enter KMS Key ARN (or press Enter to skip): ");
var kmsKeyArn = Console.ReadLine()?.Trim();

// Prompt for AWS profile (optional)
Console.Write("Enter AWS Profile name (or press Enter for default): ");
var awsProfile = Console.ReadLine()?.Trim();

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

// Configure FluentDynamoDbOptions with logger and optional encryptor
var logger = new ConsoleLogger(LogLevel.Debug);
var optionsBuilder = new FluentDynamoDbOptions()
    .WithLogger(logger);

// Configure encryption if KMS key was provided
if (!string.IsNullOrWhiteSpace(kmsKeyArn))
{
    try
    {
        var keyResolver = new DefaultKmsKeyResolver(kmsKeyArn);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        optionsBuilder = optionsBuilder.WithEncryption(encryptor);
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
    // Note: Encrypted fields would be encrypted here if encryption was implemented
    await table.SecureRecords.PutAsync(record);
    
    Console.WriteLine("─".PadRight(60, '─'));
    ConsoleHelpers.ShowSuccess($"Created record with ID: {record.Id[..8]}...");
    
    Console.WriteLine();
    Console.WriteLine("  Notice in the logs above:");
    Console.WriteLine("  - The 'email' field shows [REDACTED] (marked with [Sensitive])");
    Console.WriteLine("  - The 'creditCard' field shows [REDACTED] (marked with [Sensitive])");
    Console.WriteLine("  - The 'label' field shows the actual value (not sensitive)");
    Console.WriteLine();
    Console.WriteLine("  In DynamoDB, the actual values are stored:");
    Console.WriteLine($"  - Email: {email}");
    Console.WriteLine($"  - SSN: {(string.IsNullOrEmpty(ssn) ? "(empty)" : ssn)}");
    Console.WriteLine($"  - Credit Card: {(string.IsNullOrEmpty(creditCard) ? "(empty)" : creditCard)}");
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
