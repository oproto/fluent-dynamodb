using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a DynamoDB table class as supporting scan operations.
/// 
/// When applied to a table class, the source generator will create Scan() methods
/// that allow scanning all items in the table or index.
/// </summary>
/// <remarks>
/// <para>
/// <strong>⚠️ WARNING: Scan operations are expensive and should be used sparingly.</strong>
/// </para>
/// <para>
/// Scan operations consume read capacity for every item examined in the table,
/// not just the items that match your filter expression. This means:
/// </para>
/// <list type="bullet">
/// <item><description>A scan of a 10,000 item table consumes capacity for all 10,000 items</description></item>
/// <item><description>Filter expressions reduce data transfer but NOT consumed capacity</description></item>
/// <item><description>Scans can be slow and expensive on large tables</description></item>
/// <item><description>Scans can throttle other operations if capacity is limited</description></item>
/// </list>
/// <para>
/// <strong>Consider using Query operations instead whenever possible.</strong>
/// Query operations are more efficient because they use indexes to locate items directly.
/// </para>
/// <para>
/// Appropriate use cases for scan operations:
/// </para>
/// <list type="bullet">
/// <item><description>Data migration or ETL processes on small to medium tables</description></item>
/// <item><description>Analytics or reporting on tables with fewer than 1,000 items</description></item>
/// <item><description>Batch operations where you truly need to examine every item</description></item>
/// <item><description>One-time administrative tasks that can tolerate high latency</description></item>
/// <item><description>Development and testing scenarios with small datasets</description></item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ScannableAttribute : Attribute
{
}
