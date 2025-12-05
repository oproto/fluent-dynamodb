using System;

namespace Oproto.FluentDynamoDb.Context;

/// <summary>
/// Internal diagnostics hook that allows tests to observe operation context assignments.
/// Production code should ignore this type; it is used solely for validation scenarios.
/// </summary>
internal static class DynamoDbOperationContextDiagnostics
{
    private static event Action<OperationContextData?>? _contextAssigned;

    internal static event Action<OperationContextData?> ContextAssigned
    {
        add => _contextAssigned += value;
        remove => _contextAssigned -= value;
    }

    internal static void RaiseContextAssigned(OperationContextData? context)
    {
        _contextAssigned?.Invoke(context);
    }
}
