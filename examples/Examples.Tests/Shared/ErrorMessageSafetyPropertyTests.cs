using Examples.Shared;
using FsCheck;
using FsCheck.Xunit;

namespace Examples.Tests.Shared;

/// <summary>
/// Property-based tests for error message safety.
/// These tests verify that error messages don't expose sensitive information like stack traces.
/// </summary>
public class ErrorMessageSafetyPropertyTests
{
    /// <summary>
    /// **Feature: example-applications, Property 20: Error Message Safety**
    /// **Validates: Requirements 6.5**
    /// 
    /// For any exception with any message and stack trace, the ShowError method
    /// should only display the message without exposing the stack trace.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ShowError_DoesNotExposeStackTrace()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (message, context) =>
            {
                // Create an exception with a known message
                var exception = CreateExceptionWithStackTrace(message.Get);
                
                // Capture console output
                var originalOut = Console.Out;
                using var stringWriter = new StringWriter();
                Console.SetOut(stringWriter);
                
                try
                {
                    // Call ShowError with the exception
                    ConsoleHelpers.ShowError(exception, context.Get);
                    
                    // Get the output
                    var output = stringWriter.ToString();
                    
                    // Verify the output contains the message
                    var containsMessage = output.Contains(message.Get);
                    
                    // Verify the output does NOT contain stack trace indicators
                    var containsStackTrace = 
                        output.Contains("   at ") ||
                        output.Contains("StackTrace") ||
                        output.Contains(".cs:line") ||
                        output.Contains("CreateExceptionWithStackTrace");
                    
                    return (containsMessage && !containsStackTrace).ToProperty()
                        .Label($"ContainsMessage: {containsMessage}, ContainsStackTrace: {containsStackTrace}");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 20: Error Message Safety (continued)**
    /// **Validates: Requirements 6.5**
    /// 
    /// For any exception without context, the ShowError method should only display
    /// the exception message without exposing the stack trace.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ShowError_WithoutContext_DoesNotExposeStackTrace()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            message =>
            {
                // Create an exception with a known message
                var exception = CreateExceptionWithStackTrace(message.Get);
                
                // Capture console output
                var originalOut = Console.Out;
                using var stringWriter = new StringWriter();
                Console.SetOut(stringWriter);
                
                try
                {
                    // Call ShowError with the exception (no context)
                    ConsoleHelpers.ShowError(exception);
                    
                    // Get the output
                    var output = stringWriter.ToString();
                    
                    // Verify the output contains the message
                    var containsMessage = output.Contains(message.Get);
                    
                    // Verify the output does NOT contain stack trace indicators
                    var containsStackTrace = 
                        output.Contains("   at ") ||
                        output.Contains("StackTrace") ||
                        output.Contains(".cs:line") ||
                        output.Contains("CreateExceptionWithStackTrace");
                    
                    return (containsMessage && !containsStackTrace).ToProperty()
                        .Label($"ContainsMessage: {containsMessage}, ContainsStackTrace: {containsStackTrace}");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            });
    }

    /// <summary>
    /// Creates an exception with a real stack trace by throwing and catching it.
    /// </summary>
    private static Exception CreateExceptionWithStackTrace(string message)
    {
        try
        {
            ThrowException(message);
            return new Exception(message); // Never reached
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>
    /// Helper method to create a real stack trace.
    /// </summary>
    private static void ThrowException(string message)
    {
        throw new InvalidOperationException(message);
    }
}
