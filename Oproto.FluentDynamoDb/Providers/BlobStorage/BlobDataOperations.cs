using System.ComponentModel;

namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>Internal helper for generated code. Do not use directly.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class BlobDataOperations
{
    /// <summary>For generated code use only. Creates a BlobData instance from a reference key.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static BlobData<T> CreateFromReferenceKey<T>(
        string referenceKey,
        IBlobStorageProvider? provider,
        Func<Stream, CancellationToken, Task<T>>? deserializer)
    {
        return BlobData<T>.FromReferenceKey(referenceKey, provider, deserializer);
    }

    /// <summary>For generated code use only. Gets the pending value from a BlobData instance.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T? GetBlobPendingValue<T>(BlobData<T> blobData)
    {
        return blobData.GetPendingValue();
    }

    /// <summary>For generated code use only. Sets the reference key on a BlobData instance.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetBlobReferenceKey<T>(BlobData<T> blobData, string referenceKey)
    {
        blobData.SetReferenceKey(referenceKey);
    }

    /// <summary>For generated code use only. Sets the loaded value on a BlobData instance.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetBlobLoadedValue<T>(BlobData<T> blobData, T value)
    {
        blobData.SetLoadedValue(value);
    }
}
