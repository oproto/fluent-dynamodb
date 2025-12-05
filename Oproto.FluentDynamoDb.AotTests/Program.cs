using System;
using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.AotTests;

/// <summary>
/// AOT compatibility test program for FluentDynamoDb expression support.
/// This program verifies that expression translation works correctly in Native AOT environments.
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("=== FluentDynamoDb AOT Compatibility Tests ===");
        Console.WriteLine();

        int failureCount = 0;

        try
        {
            // Run all test suites
            failureCount += ClosureCaptureTests.Run();
            failureCount += ExpressionTranslationTests.Run();
            failureCount += GenericMethodTests.Run();
            failureCount += TrimmingCompatibilityTests.Run();
            failureCount += UpdateExpressionAotTests.Run();

            Console.WriteLine();
            if (failureCount == 0)
            {
                Console.WriteLine("✓ All AOT compatibility tests passed!");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ {failureCount} test(s) failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
