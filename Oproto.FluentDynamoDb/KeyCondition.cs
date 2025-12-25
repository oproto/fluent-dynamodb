namespace Oproto.FluentDynamoDb;

/// <summary>
/// Specifies automatic key attribute existence conditions for DynamoDB operations.
/// Use with Put, Update, and Delete operations to simplify common conditional patterns.
/// </summary>
/// <remarks>
/// <para>
/// Key conditions automatically generate <c>attribute_exists()</c> or <c>attribute_not_exists()</c>
/// conditions for all key attributes (partition key and sort key if present).
/// </para>
/// <para>
/// For entities with only a partition key, generates conditions like <c>attribute_exists(pk)</c>.
/// For entities with composite keys, generates conditions like <c>attribute_exists(pk) AND attribute_exists(sk)</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create only (fail if exists)
/// await table.Users.Put(user).IfNotExists().PutAsync();
/// await table.Users.PutAsync(user, KeyCondition.MustNotExist);
/// 
/// // Update existing only (prevent upsert)
/// await table.Users.Update(pk, sk, KeyCondition.MustExist)
///     .Set(x => new UserUpdateModel { Name = newName })
///     .UpdateAsync();
/// </code>
/// </example>
public enum KeyCondition
{
    /// <summary>
    /// No automatic condition is added. Default behavior.
    /// The operation proceeds without any key existence check.
    /// </summary>
    None = 0,

    /// <summary>
    /// Adds <c>attribute_exists()</c> conditions for all key attributes.
    /// The operation fails with <see cref="Amazon.DynamoDBv2.Model.ConditionalCheckFailedException"/>
    /// if the item does not exist.
    /// </summary>
    /// <remarks>
    /// <para>For simple key entities: generates <c>attribute_exists(pk)</c></para>
    /// <para>For composite key entities: generates <c>attribute_exists(pk) AND attribute_exists(sk)</c></para>
    /// </remarks>
    MustExist = 1,

    /// <summary>
    /// Adds <c>attribute_not_exists()</c> conditions for all key attributes.
    /// The operation fails with <see cref="Amazon.DynamoDBv2.Model.ConditionalCheckFailedException"/>
    /// if the item already exists.
    /// </summary>
    /// <remarks>
    /// <para>For simple key entities: generates <c>attribute_not_exists(pk)</c></para>
    /// <para>For composite key entities: generates <c>attribute_not_exists(pk) AND attribute_not_exists(sk)</c></para>
    /// </remarks>
    MustNotExist = 2
}
