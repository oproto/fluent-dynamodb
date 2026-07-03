# Bugfix Requirements Document

## Introduction

The source generator emits hydrator/mapper code that calls `internal` methods on `BlobData<T>` (`FromReferenceKey`, `SetReferenceKey`, `GetPendingValue`, `SetLoadedValue`). Since generated code runs in the consuming assembly — not in the library assembly — external NuGet consumers cannot access these `internal` members, resulting in CS1061/CS0117 compile errors. This makes the `[BlobStorage]` attribute with `BlobData<T>` properties a broken feature for any external consumer.

The fix creates a public static helper class in the library that wraps these internal operations, and updates the source generator to emit calls to the public helpers instead of the internal methods directly.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an external consuming assembly defines an entity with `[BlobStorage]` on a `BlobData<T>` property AND the source generator emits code calling `BlobData<T>.FromReferenceKey(...)` THEN the system produces a CS0117 compile error because `FromReferenceKey` is inaccessible due to its `internal` access modifier

1.2 WHEN an external consuming assembly defines an entity with `[BlobStorage]` on a `BlobData<T>` property AND the source generator emits code calling `blobInstance.SetReferenceKey(...)` THEN the system produces a CS1061 compile error because `SetReferenceKey` is inaccessible due to its `internal` access modifier

1.3 WHEN an external consuming assembly defines an entity with `[BlobStorage]` on a `BlobData<T>` property AND the source generator emits code calling `blobInstance.GetPendingValue()` THEN the system produces a CS1061 compile error because `GetPendingValue` is inaccessible due to its `internal` access modifier

1.4 WHEN an external consuming assembly defines an entity with `[BlobStorage]` on a `BlobData<T>` property AND the source generator emits code calling `blobInstance.SetLoadedValue(...)` THEN the system produces a CS1061 compile error because `SetLoadedValue` is inaccessible due to its `internal` access modifier

### Expected Behavior (Correct)

2.1 WHEN an external consuming assembly defines an entity with `[BlobStorage]` on a `BlobData<T>` property AND the source generator emits deserialization code THEN the system SHALL generate a call to a public helper method (e.g., `BlobDataOperations.CreateFromReferenceKey<T>(...)`) that internally delegates to `BlobData<T>.FromReferenceKey(...)`, and the project SHALL compile without errors

2.2 WHEN an external consuming assembly defines an entity with `[BlobStorage]` on a `BlobData<T>` property AND the source generator emits code to set the reference key after blob upload THEN the system SHALL generate a call to a public helper method (e.g., `BlobDataOperations.SetBlobReferenceKey<T>(...)`) that internally delegates to `BlobData<T>.SetReferenceKey(...)`, and the project SHALL compile without errors

2.3 WHEN an external consuming assembly defines an entity with `[BlobStorage]` on a `BlobData<T>` property AND the source generator emits serialization code to retrieve pending data THEN the system SHALL generate a call to a public helper method (e.g., `BlobDataOperations.GetBlobPendingValue<T>(...)`) that internally delegates to `BlobData<T>.GetPendingValue()`, and the project SHALL compile without errors

2.4 WHEN an external consuming assembly defines an entity with `[BlobStorage]` on a `BlobData<T>` property AND the source generator emits eager-loading deserialization code THEN the system SHALL generate a call to a public helper method (e.g., `BlobDataOperations.SetBlobLoadedValue<T>(...)`) that internally delegates to `BlobData<T>.SetLoadedValue(...)`, and the project SHALL compile without errors

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a consuming assembly uses `BlobData<T>.Create(value)` to create new blob data THEN the system SHALL CONTINUE TO create a `BlobData<T>` instance with `IsLoaded = true` and `HasPendingData = true`

3.2 WHEN a consuming assembly calls `blobInstance.LoadAsync()` to lazy-load blob data THEN the system SHALL CONTINUE TO retrieve data from the configured blob storage provider and set `IsLoaded = true`

3.3 WHEN a consuming assembly accesses `blobInstance.Value` after loading THEN the system SHALL CONTINUE TO return the loaded data value

3.4 WHEN a consuming assembly accesses `blobInstance.ReferenceKey` THEN the system SHALL CONTINUE TO return the stored reference key

3.5 WHEN a consuming assembly accesses `blobInstance.HasPendingData` THEN the system SHALL CONTINUE TO return whether there is pending data to upload

3.6 WHEN a consuming assembly uses entities with `[BlobStorage]` attributes and the existing unit tests run (which have `InternalsVisibleTo` access) THEN the system SHALL CONTINUE TO pass all existing blob storage unit tests without modification

3.7 WHEN the public helper methods are introduced THEN they SHALL NOT appear in IntelliSense for normal consumers (marked with `[EditorBrowsable(EditorBrowsableState.Never)]`) and SHALL include XML documentation indicating they are for generated code use only
