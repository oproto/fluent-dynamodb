using Amazon.Lambda.DynamoDBEvents;

namespace Oproto.FluentDynamoDb.Streams;

public class DynamoDbStreamRecordEventProcessor
{
    
    public required DynamoDBEvent.DynamodbStreamRecord Record { get; init; }

    public async Task<DynamoDbStreamRecordEventProcessor> Awaitable()
    {
        return await Task.Run(() => this);
    }
}

public static class DynamoDbStreamRecordEventProcessorExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="processor"></param>
    /// <param name="onInsertFunc"></param>
    /// <returns></returns>
    public static async Task<DynamoDbStreamRecordEventProcessor> OnInsert(this Task<DynamoDbStreamRecordEventProcessor> processor, Func<DynamoDBEvent.DynamodbStreamRecord, Task> onInsertFunc)
    {
        await processor;
        if (processor.Result.Record.EventName == "INSERT")
        {
            await onInsertFunc(processor.Result.Record);
        }
        return processor.Result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="processor"></param>
    /// <param name="onUpdateFunc"></param>
    /// <returns></returns>
    public static async Task<DynamoDbStreamRecordEventProcessor> OnUpdate(this Task<DynamoDbStreamRecordEventProcessor> processor, Func<DynamoDBEvent.DynamodbStreamRecord, Task> onUpdateFunc)
    {
        await processor;
        if (processor.Result.Record.EventName == "UPDATE")
        {
            await onUpdateFunc(processor.Result.Record);
        }
        return processor.Result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="processor"></param>
    /// <param name="onDeleteFunc"></param>
    /// <returns></returns>
    public static async Task<DynamoDbStreamRecordEventProcessor> OnDelete(this Task<DynamoDbStreamRecordEventProcessor> processor, Func<DynamoDBEvent.DynamodbStreamRecord, Task> onDeleteFunc)
    {
        await processor;
        if (processor.Result.Record.EventName == "REMOVE")
        {
            await onDeleteFunc(processor.Result.Record);
        }
        return processor.Result;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="processor"></param>
    /// <param name="onDeleteFunc"></param>
    /// <returns></returns>
    public static async Task<DynamoDbStreamRecordEventProcessor> OnNonTtlDelete(this Task<DynamoDbStreamRecordEventProcessor> processor, Func<DynamoDBEvent.DynamodbStreamRecord, Task> onDeleteFunc)
    {
        await processor;
        if (processor.Result.Record.EventName == "REMOVE" && !IsTtlUser(processor.Result.Record.UserIdentity))
        {
            await onDeleteFunc(processor.Result.Record);
        }
        return processor.Result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="processor"></param>
    /// <param name="onDeleteFunc"></param>
    /// <returns></returns>
    public static async Task<DynamoDbStreamRecordEventProcessor> OnTtlDelete(this Task<DynamoDbStreamRecordEventProcessor> processor, Func<DynamoDBEvent.DynamodbStreamRecord, Task> onDeleteFunc)
    {
        await processor;
        if (processor.Result.Record.EventName == "REMOVE" && IsTtlUser(processor.Result.Record.UserIdentity))
        {
            await onDeleteFunc(processor.Result.Record);
        }
        return processor.Result;
    }

    private static bool IsTtlUser(DynamoDBEvent.Identity identity)
    {
        return identity is { Type: "Service", PrincipalId: "dynamodb.amazonaws.com" };
    }
}