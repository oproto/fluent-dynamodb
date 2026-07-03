using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

/// <summary>
/// Preservation property tests for BlobData&lt;T&gt; public API behavior.
/// These tests document and verify the baseline behavior BEFORE the BlobDataOperations fix,
/// ensuring no regressions occur after the fix is applied.
/// 
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**
/// </summary>
public class BlobDataPreservationPropertyTests
{
    /// <summary>
    /// **Property 2: Preservation** - Create factory method state invariants.
    /// 
    /// For all string values v, BlobData&lt;string&gt;.Create(v) yields
    /// IsLoaded == true, HasPendingData == true, Value == v.
    /// 
    /// **Validates: Requirements 3.1, 3.3, 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Create_YieldsCorrectState_ForAllStringValues()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            value =>
            {
                // Act
                var blobData = BlobData<string>.Create(value);

                // Assert
                var isLoaded = blobData.IsLoaded;
                var hasPendingData = blobData.HasPendingData;
                var valueMatches = blobData.Value == value;

                return (isLoaded && hasPendingData && valueMatches).ToProperty()
                    .Label($"Create(\"{value}\") should yield IsLoaded=true, HasPendingData=true, Value=input. " +
                           $"IsLoaded: {isLoaded}, HasPendingData: {hasPendingData}, ValueMatches: {valueMatches}");
            });
    }

    /// <summary>
    /// **Property 2: Preservation** - FromReferenceKey state invariants.
    /// 
    /// For all string values key, after creating a BlobData via FromReferenceKey(key, null, null),
    /// ReferenceKey == key and IsLoaded == false.
    /// 
    /// **Validates: Requirements 3.4, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FromReferenceKey_YieldsCorrectState_ForAllStringKeys()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            key =>
            {
                // Act
                var blobData = BlobData<string>.FromReferenceKey(key.Get, null, null);

                // Assert
                var referenceKeyMatches = blobData.ReferenceKey == key.Get;
                var isNotLoaded = !blobData.IsLoaded;
                var noPendingData = !blobData.HasPendingData;

                return (referenceKeyMatches && isNotLoaded && noPendingData).ToProperty()
                    .Label($"FromReferenceKey(\"{key.Get}\", null, null) should yield ReferenceKey=key, IsLoaded=false, HasPendingData=false. " +
                           $"ReferenceKeyMatches: {referenceKeyMatches}, IsNotLoaded: {isNotLoaded}, NoPendingData: {noPendingData}");
            });
    }

    /// <summary>
    /// **Property 2: Preservation** - GetPendingValue returns value when HasPendingData is true.
    /// 
    /// For all non-null instances created via Create(v), GetPendingValue() returns v.
    /// 
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetPendingValue_ReturnsValue_WhenCreatedViaCreate()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            value =>
            {
                // Arrange
                var blobData = BlobData<string>.Create(value);

                // Act
                var pendingValue = blobData.GetPendingValue();

                // Assert
                var hasPendingData = blobData.HasPendingData;
                var pendingValueMatches = pendingValue == value;

                return (hasPendingData && pendingValueMatches).ToProperty()
                    .Label($"GetPendingValue() should return the created value when HasPendingData=true. " +
                           $"HasPendingData: {hasPendingData}, PendingValueMatches: {pendingValueMatches}, " +
                           $"Expected: \"{value}\", Got: \"{pendingValue}\"");
            });
    }

    /// <summary>
    /// **Property 2: Preservation** - GetPendingValue returns default when HasPendingData is false.
    /// 
    /// For all instances not created via Create (i.e., created via FromReferenceKey),
    /// GetPendingValue() returns default.
    /// 
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetPendingValue_ReturnsDefault_WhenNotCreatedViaCreate()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            key =>
            {
                // Arrange - create via FromReferenceKey (not Create)
                var blobData = BlobData<string>.FromReferenceKey(key.Get, null, null);

                // Act
                var pendingValue = blobData.GetPendingValue();

                // Assert
                var noPendingData = !blobData.HasPendingData;
                var pendingValueIsDefault = pendingValue == default;

                return (noPendingData && pendingValueIsDefault).ToProperty()
                    .Label($"GetPendingValue() should return default when HasPendingData=false. " +
                           $"NoPendingData: {noPendingData}, PendingValueIsDefault: {pendingValueIsDefault}, " +
                           $"Got: \"{pendingValue}\"");
            });
    }

    /// <summary>
    /// **Property 2: Preservation** - SetReferenceKey sets key and clears pending data.
    /// 
    /// For all string values key, after calling SetReferenceKey(key) on a BlobData created via Create,
    /// ReferenceKey == key and HasPendingData == false.
    /// 
    /// **Validates: Requirements 3.4, 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SetReferenceKey_SetsKeyAndClearsPendingData()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.NonEmptyString(),
            (value, key) =>
            {
                // Arrange
                var blobData = BlobData<string>.Create(value.Get);
                var hadPendingDataBefore = blobData.HasPendingData;

                // Act
                blobData.SetReferenceKey(key.Get);

                // Assert
                var referenceKeyMatches = blobData.ReferenceKey == key.Get;
                var noPendingDataAfter = !blobData.HasPendingData;
                var stillLoaded = blobData.IsLoaded;

                return (hadPendingDataBefore && referenceKeyMatches && noPendingDataAfter && stillLoaded).ToProperty()
                    .Label($"SetReferenceKey should set key and clear pending data. " +
                           $"HadPendingDataBefore: {hadPendingDataBefore}, ReferenceKeyMatches: {referenceKeyMatches}, " +
                           $"NoPendingDataAfter: {noPendingDataAfter}, StillLoaded: {stillLoaded}");
            });
    }

    /// <summary>
    /// **Property 2: Preservation** - SetLoadedValue sets value and marks as loaded.
    /// 
    /// For all string values, after calling SetLoadedValue(value) on a BlobData created via
    /// FromReferenceKey, IsLoaded == true and Value == value.
    /// 
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SetLoadedValue_SetsValueAndMarksLoaded()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.NonEmptyString(),
            (key, value) =>
            {
                // Arrange - create unloaded instance
                var blobData = BlobData<string>.FromReferenceKey(key.Get, null, null);
                var wasNotLoadedBefore = !blobData.IsLoaded;

                // Act
                blobData.SetLoadedValue(value.Get);

                // Assert
                var isLoadedAfter = blobData.IsLoaded;
                var valueMatches = blobData.Value == value.Get;

                return (wasNotLoadedBefore && isLoadedAfter && valueMatches).ToProperty()
                    .Label($"SetLoadedValue should mark as loaded with correct value. " +
                           $"WasNotLoadedBefore: {wasNotLoadedBefore}, IsLoadedAfter: {isLoadedAfter}, " +
                           $"ValueMatches: {valueMatches}");
            });
    }
}
