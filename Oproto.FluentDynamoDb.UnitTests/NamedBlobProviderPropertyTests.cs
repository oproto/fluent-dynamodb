using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace Oproto.FluentDynamoDb.UnitTests;

/// <summary>
/// Property-based tests for named blob provider registration and resolution.
/// Each test runs 25 iterations with random inputs to verify universal properties.
/// </summary>
public class NamedBlobProviderPropertyTests
{
    /// <summary>
    /// Property 1: Registration Round-Trip
    /// For any valid provider name, registering via WithBlobStorage(name, provider) then calling
    /// GetBlobProvider(name) returns the same instance.
    /// **Validates: Requirements 1.1, 1.2, 2.1**
    /// </summary>
    [Property(MaxTest = 25)]
    public Property RegistrationRoundTrip()
    {
        var validNameArb = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(
            validNameArb,
            name =>
            {
                // Arrange
                var provider = Substitute.For<IBlobStorageProvider>();

                // Act
                var options = new FluentDynamoDbOptions()
                    .WithBlobStorage(name, provider);
                var resolved = options.GetBlobProvider(name);

                // Assert
                return ReferenceEquals(resolved, provider).ToProperty()
                    .Label($"GetBlobProvider(\"{name}\") should return the same instance that was registered");
            });
    }

    /// <summary>
    /// Property 2: Invalid Name Rejection
    /// For null, empty, or whitespace-only strings, WithBlobStorage(name, provider) throws ArgumentException.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 25)]
    public Property InvalidName_ThrowsArgumentException()
    {
        // Generate strings that are null, empty, or whitespace-only
        var invalidNameArb = Gen.OneOf(
            Gen.Constant<string>(null!),
            Gen.Constant(string.Empty),
            Gen.Elements(' ', '\t', '\n', '\r')
                .ListOf()
                .Where(chars => chars.Count > 0)
                .Select(chars => new string(chars.ToArray()))
        ).ToArbitrary();

        return Prop.ForAll(
            invalidNameArb,
            invalidName =>
            {
                // Arrange
                var provider = Substitute.For<IBlobStorageProvider>();

                // Act & Assert
                try
                {
                    new FluentDynamoDbOptions().WithBlobStorage(invalidName, provider);
                    return false.ToProperty()
                        .Label($"Expected ArgumentException for name: \"{invalidName ?? "null"}\"");
                }
                catch (ArgumentException)
                {
                    return true.ToProperty();
                }
            });
    }

    /// <summary>
    /// Property 3: Replacement Semantics (Last Registration Wins)
    /// For any valid name and two distinct providers A and B, registering A then B under the same name
    /// results in GetBlobProvider(name) returning B.
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 25)]
    public Property ReplacementSemantics_LastRegistrationWins()
    {
        var validNameArb = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(
            validNameArb,
            name =>
            {
                // Arrange
                var providerA = Substitute.For<IBlobStorageProvider>();
                var providerB = Substitute.For<IBlobStorageProvider>();

                // Act - register A, then B under the same name
                var options = new FluentDynamoDbOptions()
                    .WithBlobStorage(name, providerA)
                    .WithBlobStorage(name, providerB);

                var resolved = options.GetBlobProvider(name);

                // Assert - last registration wins
                return ReferenceEquals(resolved, providerB).ToProperty()
                    .Label($"GetBlobProvider(\"{name}\") should return the last registered provider (B)");
            });
    }

    /// <summary>
    /// Property 4: Missing Provider Error with Diagnostic Info
    /// For registered names and a name not in the set, GetBlobProvider(missingName) throws
    /// InvalidOperationException containing the requested name and listing available providers.
    /// **Validates: Requirements 2.3, 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 25)]
    public Property MissingProvider_ThrowsWithDiagnosticInfo()
    {
        // Generate 1-5 distinct valid names for registration, plus one distinct missing name
        var testDataArb = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .ListOf()
            .Select(list => list.Select(s => s.Get).Distinct().ToList())
            .Where(list => list.Count >= 2) // Need at least 1 registered + 1 missing
            .Select(list =>
            {
                var registered = list.Take(list.Count - 1).ToList();
                var missing = list.Last();
                return (registered, missing);
            })
            .ToArbitrary();

        return Prop.ForAll(
            testDataArb,
            data =>
            {
                var (registeredNames, missingName) = data;

                // Arrange - register providers for all names except the missing one
                var options = new FluentDynamoDbOptions();
                foreach (var name in registeredNames)
                {
                    options = options.WithBlobStorage(name, Substitute.For<IBlobStorageProvider>());
                }

                // Act & Assert
                try
                {
                    options.GetBlobProvider(missingName);
                    return false.ToProperty()
                        .Label($"Expected InvalidOperationException for missing name: \"{missingName}\"");
                }
                catch (InvalidOperationException ex)
                {
                    var containsRequestedName = ex.Message.Contains(missingName);
                    var listsAvailableProviders = registeredNames.All(n => ex.Message.Contains(n));

                    return (containsRequestedName && listsAvailableProviders).ToProperty()
                        .Label($"Exception message should contain requested name '{missingName}' " +
                               $"and list all available providers. Message: {ex.Message}");
                }
            });
    }

    /// <summary>
    /// Property 5: Registration Preservation Through Chaining
    /// For any sequence of registrations (one default + N named), the final instance exposes all via GetBlobProvider.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 25)]
    public Property RegistrationPreservation_ThroughChaining()
    {
        // Generate 1-5 distinct valid provider names
        var distinctNamesArb = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .ListOf()
            .Select(list => list
                .Select(s => s.Get)
                .Distinct()
                .Take(5)
                .ToList())
            .Where(list => list.Count >= 1)
            .ToArbitrary();

        return Prop.ForAll(
            distinctNamesArb,
            names =>
            {
                // Arrange
                var defaultProvider = Substitute.For<IBlobStorageProvider>();
                var namedProviders = names.ToDictionary(
                    name => name,
                    _ => Substitute.For<IBlobStorageProvider>());

                // Act - chain registrations: default first, then all named
                var options = new FluentDynamoDbOptions()
                    .WithBlobStorage(defaultProvider);

                foreach (var kvp in namedProviders)
                {
                    options = options.WithBlobStorage(kvp.Key, kvp.Value);
                }

                // Assert - default provider is accessible via GetBlobProvider(null)
                var defaultResolved = ReferenceEquals(options.GetBlobProvider(null), defaultProvider);

                // Assert - each named provider is accessible via GetBlobProvider(name)
                var allNamedResolved = namedProviders.All(kvp =>
                    ReferenceEquals(options.GetBlobProvider(kvp.Key), kvp.Value));

                return (defaultResolved && allNamedResolved).ToProperty()
                    .Label($"All registrations should be preserved through chaining. " +
                           $"DefaultResolved: {defaultResolved}, AllNamedResolved: {allNamedResolved}, " +
                           $"NameCount: {names.Count}");
            });
    }

    /// <summary>
    /// Property 6: Copy-on-Write Immutability
    /// For any instance with existing registrations, calling WithBlobStorage(name, provider) returns a
    /// new instance without mutating the original.
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Property(MaxTest = 25)]
    public Property CopyOnWrite_OriginalNotMutated()
    {
        // Generate 1-3 distinct valid names for initial registrations, plus one new name
        var testDataArb = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .ListOf()
            .Select(list => list.Select(s => s.Get).Distinct().ToList())
            .Where(list => list.Count >= 2) // Need at least 1 existing + 1 new
            .Select(list =>
            {
                var existing = list.Take(list.Count - 1).ToList();
                var newName = list.Last();
                return (existing, newName);
            })
            .ToArbitrary();

        return Prop.ForAll(
            testDataArb,
            data =>
            {
                var (existingNames, newName) = data;

                // Arrange - build an initial options instance with existing registrations
                var existingProviders = existingNames.ToDictionary(
                    name => name,
                    _ => Substitute.For<IBlobStorageProvider>());

                var original = new FluentDynamoDbOptions();
                foreach (var kvp in existingProviders)
                {
                    original = original.WithBlobStorage(kvp.Key, kvp.Value);
                }

                // Act - add a new registration, producing a new instance
                var newProvider = Substitute.For<IBlobStorageProvider>();
                var modified = original.WithBlobStorage(newName, newProvider);

                // Assert 1: modified is a different instance
                var isDifferentInstance = !ReferenceEquals(original, modified);

                // Assert 2: original does NOT have access to the newly registered provider
                bool originalLacksNewProvider;
                try
                {
                    original.GetBlobProvider(newName);
                    originalLacksNewProvider = false; // Should have thrown
                }
                catch (InvalidOperationException)
                {
                    originalLacksNewProvider = true;
                }

                // Assert 3: original's existing registrations remain intact
                var originalIntact = existingProviders.All(kvp =>
                    ReferenceEquals(original.GetBlobProvider(kvp.Key), kvp.Value));

                // Assert 4: modified has both existing and new registrations
                var modifiedHasNew = ReferenceEquals(modified.GetBlobProvider(newName), newProvider);
                var modifiedHasExisting = existingProviders.All(kvp =>
                    ReferenceEquals(modified.GetBlobProvider(kvp.Key), kvp.Value));

                return (isDifferentInstance && originalLacksNewProvider && originalIntact
                        && modifiedHasNew && modifiedHasExisting).ToProperty()
                    .Label($"Copy-on-write violated. isDifferentInstance: {isDifferentInstance}, " +
                           $"originalLacksNewProvider: {originalLacksNewProvider}, " +
                           $"originalIntact: {originalIntact}, " +
                           $"modifiedHasNew: {modifiedHasNew}, " +
                           $"modifiedHasExisting: {modifiedHasExisting}");
            });
    }
}
