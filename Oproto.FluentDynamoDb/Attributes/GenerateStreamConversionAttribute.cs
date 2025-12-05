namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a DynamoDB entity class to generate stream conversion methods for processing DynamoDB Streams events.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is an opt-in mechanism for generating <c>FromDynamoDbStream</c> methods that deserialize
/// DynamoDB Stream records (using <c>Amazon.Lambda.DynamoDBEvents.AttributeValue</c>) into strongly-typed entities.
/// </para>
/// <para>
/// <strong>Requirements:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>The <c>Oproto.FluentDynamoDb.Streams</c> package must be referenced</description></item>
/// <item><description>The <c>Amazon.Lambda.DynamoDBEvents</c> package must be referenced (version 3.1.1+)</description></item>
/// <item><description>The entity class must also have <c>[DynamoDbTable]</c> or <c>[DynamoDbEntity]</c> attribute</description></item>
/// </list>
/// <para>
/// <strong>Generated Methods:</strong>
/// </para>
/// <list type="bullet">
/// <item><description><c>FromDynamoDbStream(Dictionary&lt;string, Lambda.AttributeValue&gt;)</c> - Deserializes a stream image dictionary</description></item>
/// <item><description><c>FromStreamImage(StreamRecord, bool)</c> - Helper to extract NewImage or OldImage from a stream record</description></item>
/// </list>
/// <para>
/// Stream conversion methods support the same features as standard DynamoDB mapping:
/// </para>
/// <list type="bullet">
/// <item><description>Field-level encryption (using <c>[Encrypted]</c> attribute)</description></item>
/// <item><description>Discriminator validation (using <c>DiscriminatorProperty</c> and <c>DiscriminatorValue</c>)</description></item>
/// <item><description>Type conversions and nullable properties</description></item>
/// <item><description>AOT-compatible code generation</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para><strong>Basic usage:</strong></para>
/// <code>
/// [DynamoDbTable("Users")]
/// [GenerateStreamConversion]
/// public partial class User
/// {
///     [PartitionKey] public string UserId { get; set; }
///     public string Name { get; set; }
///     public string Email { get; set; }
/// }
/// 
/// // In Lambda handler:
/// public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
/// {
///     foreach (var record in dynamoEvent.Records)
///     {
///         await record.Process&lt;User&gt;()
///             .OnInsert(async (_, newUser) =&gt; 
///             {
///                 await _emailService.SendWelcomeEmail(newUser.Email);
///             })
///             .ProcessAsync();
///     }
/// }
/// </code>
/// <para><strong>With encryption:</strong></para>
/// <code>
/// [DynamoDbTable("Users")]
/// [GenerateStreamConversion]
/// public partial class User
/// {
///     [PartitionKey] public string UserId { get; set; }
///     public string Name { get; set; }
///     
///     [Encrypted]
///     public string SocialSecurityNumber { get; set; }
/// }
/// 
/// // Encrypted fields are automatically decrypted when processing streams
/// </code>
/// <para><strong>With discriminators:</strong></para>
/// <code>
/// [DynamoDbTable("MyTable", 
///     DiscriminatorProperty = "EntityType",
///     DiscriminatorValue = "User")]
/// [GenerateStreamConversion]
/// public partial class UserEntity
/// {
///     [PartitionKey] public string PK { get; set; }
///     [SortKey] public string SK { get; set; }
///     public string Name { get; set; }
/// }
/// 
/// // Discriminator validation is automatically performed during deserialization
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateStreamConversionAttribute : Attribute
{
}
