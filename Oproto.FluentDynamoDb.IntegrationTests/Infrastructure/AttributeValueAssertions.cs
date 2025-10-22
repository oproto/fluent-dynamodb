using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Provides assertion helpers for comparing AttributeValue objects and dictionaries.
/// These helpers perform deep equality comparisons that handle DynamoDB's complex type system.
/// </summary>
public static class AttributeValueAssertions
{
    /// <summary>
    /// Asserts that two AttributeValue dictionaries are deeply equal.
    /// Compares all attributes including nested structures, sets, lists, and maps.
    /// </summary>
    /// <param name="actual">The actual AttributeValue dictionary.</param>
    /// <param name="expected">The expected AttributeValue dictionary.</param>
    /// <param name="because">Optional reason for the assertion.</param>
    public static void ShouldBeEquivalentTo(
        this Dictionary<string, AttributeValue> actual,
        Dictionary<string, AttributeValue> expected,
        string because = "")
    {
        using (new AssertionScope())
        {
            actual.Should().NotBeNull("actual dictionary should not be null");
            expected.Should().NotBeNull("expected dictionary should not be null");
            
            // Check that both dictionaries have the same keys
            actual.Keys.Should().BeEquivalentTo(expected.Keys, 
                $"dictionaries should have the same keys {because}");
            
            // Compare each attribute value
            foreach (var key in expected.Keys)
            {
                if (!actual.ContainsKey(key))
                {
                    throw new AssertionFailedException(
                        $"Expected key '{key}' not found in actual dictionary {because}");
                }
                
                actual[key].ShouldBeEquivalentTo(expected[key], 
                    $"attribute '{key}' should match {because}");
            }
        }
    }
    
    /// <summary>
    /// Asserts that two AttributeValue objects are deeply equal.
    /// Handles all DynamoDB attribute types including strings, numbers, binary, sets, lists, and maps.
    /// </summary>
    /// <param name="actual">The actual AttributeValue.</param>
    /// <param name="expected">The expected AttributeValue.</param>
    /// <param name="because">Optional reason for the assertion.</param>
    public static void ShouldBeEquivalentTo(
        this AttributeValue actual,
        AttributeValue expected,
        string because = "")
    {
        using (new AssertionScope())
        {
            actual.Should().NotBeNull("actual AttributeValue should not be null");
            expected.Should().NotBeNull("expected AttributeValue should not be null");
            
            // String (S)
            if (expected.S != null)
            {
                actual.S.Should().Be(expected.S, $"string values should match {because}");
                return;
            }
            
            // Number (N)
            if (expected.N != null)
            {
                actual.N.Should().Be(expected.N, $"number values should match {because}");
                return;
            }
            
            // Binary (B)
            if (expected.B != null)
            {
                actual.B.Should().NotBeNull($"binary value should not be null {because}");
                actual.B.ToArray().Should().BeEquivalentTo(expected.B.ToArray(), 
                    $"binary values should match {because}");
                return;
            }
            
            // Boolean (BOOL)
            if (expected.IsBOOLSet)
            {
                actual.BOOL.Should().Be(expected.BOOL, $"boolean values should match {because}");
                return;
            }
            
            // Null (NULL)
            if (expected.NULL)
            {
                actual.NULL.Should().BeTrue($"null flag should be true {because}");
                return;
            }
            
            // String Set (SS)
            if (expected.SS?.Count > 0)
            {
                actual.SS.Should().NotBeNull($"string set should not be null {because}");
                actual.SS.Should().BeEquivalentTo(expected.SS, 
                    $"string set values should match {because}");
                return;
            }
            
            // Number Set (NS)
            if (expected.NS?.Count > 0)
            {
                actual.NS.Should().NotBeNull($"number set should not be null {because}");
                actual.NS.Should().BeEquivalentTo(expected.NS, 
                    $"number set values should match {because}");
                return;
            }
            
            // Binary Set (BS)
            if (expected.BS?.Count > 0)
            {
                actual.BS.Should().NotBeNull($"binary set should not be null {because}");
                actual.BS.Count.Should().Be(expected.BS.Count, 
                    $"binary set should have same count {because}");
                
                for (var i = 0; i < expected.BS.Count; i++)
                {
                    actual.BS[i].ToArray().Should().BeEquivalentTo(expected.BS[i].ToArray(),
                        $"binary set element {i} should match {because}");
                }
                return;
            }
            
            // List (L)
            if (expected.L?.Count > 0)
            {
                actual.L.Should().NotBeNull($"list should not be null {because}");
                actual.L.Count.Should().Be(expected.L.Count, 
                    $"list should have same count {because}");
                
                for (var i = 0; i < expected.L.Count; i++)
                {
                    actual.L[i].ShouldBeEquivalentTo(expected.L[i], 
                        $"list element {i} should match {because}");
                }
                return;
            }
            
            // Map (M)
            if (expected.M?.Count > 0)
            {
                actual.M.Should().NotBeNull($"map should not be null {because}");
                actual.M.ShouldBeEquivalentTo(expected.M, 
                    $"map values should match {because}");
                return;
            }
            
            // If we get here, the expected value has no set properties
            throw new AssertionFailedException(
                $"Expected AttributeValue has no set properties {because}");
        }
    }
    
    /// <summary>
    /// Asserts that an AttributeValue dictionary contains a specific key with a string value.
    /// </summary>
    /// <param name="item">The AttributeValue dictionary.</param>
    /// <param name="key">The key to check for.</param>
    /// <param name="expectedValue">The expected string value.</param>
    /// <param name="because">Optional reason for the assertion.</param>
    public static void ShouldContainStringAttribute(
        this Dictionary<string, AttributeValue> item,
        string key,
        string expectedValue,
        string because = "")
    {
        item.Should().ContainKey(key, $"item should contain key '{key}' {because}");
        item[key].S.Should().Be(expectedValue, 
            $"attribute '{key}' should have string value '{expectedValue}' {because}");
    }
    
    /// <summary>
    /// Asserts that an AttributeValue dictionary contains a specific key with a number value.
    /// </summary>
    /// <param name="item">The AttributeValue dictionary.</param>
    /// <param name="key">The key to check for.</param>
    /// <param name="expectedValue">The expected number value as a string.</param>
    /// <param name="because">Optional reason for the assertion.</param>
    public static void ShouldContainNumberAttribute(
        this Dictionary<string, AttributeValue> item,
        string key,
        string expectedValue,
        string because = "")
    {
        item.Should().ContainKey(key, $"item should contain key '{key}' {because}");
        item[key].N.Should().Be(expectedValue, 
            $"attribute '{key}' should have number value '{expectedValue}' {because}");
    }
    
    /// <summary>
    /// Asserts that an AttributeValue dictionary contains a specific key with a string set.
    /// </summary>
    /// <param name="item">The AttributeValue dictionary.</param>
    /// <param name="key">The key to check for.</param>
    /// <param name="expectedValues">The expected string set values.</param>
    /// <param name="because">Optional reason for the assertion.</param>
    public static void ShouldContainStringSet(
        this Dictionary<string, AttributeValue> item,
        string key,
        IEnumerable<string> expectedValues,
        string because = "")
    {
        item.Should().ContainKey(key, $"item should contain key '{key}' {because}");
        item[key].SS.Should().BeEquivalentTo(expectedValues, 
            $"attribute '{key}' should have string set values {because}");
    }
    
    /// <summary>
    /// Asserts that an AttributeValue dictionary contains a specific key with a number set.
    /// </summary>
    /// <param name="item">The AttributeValue dictionary.</param>
    /// <param name="key">The key to check for.</param>
    /// <param name="expectedValues">The expected number set values as strings.</param>
    /// <param name="because">Optional reason for the assertion.</param>
    public static void ShouldContainNumberSet(
        this Dictionary<string, AttributeValue> item,
        string key,
        IEnumerable<string> expectedValues,
        string because = "")
    {
        item.Should().ContainKey(key, $"item should contain key '{key}' {because}");
        item[key].NS.Should().BeEquivalentTo(expectedValues, 
            $"attribute '{key}' should have number set values {because}");
    }
    
    /// <summary>
    /// Asserts that an AttributeValue dictionary contains a specific key with a list.
    /// </summary>
    /// <param name="item">The AttributeValue dictionary.</param>
    /// <param name="key">The key to check for.</param>
    /// <param name="expectedCount">The expected number of elements in the list.</param>
    /// <param name="because">Optional reason for the assertion.</param>
    public static void ShouldContainList(
        this Dictionary<string, AttributeValue> item,
        string key,
        int expectedCount,
        string because = "")
    {
        item.Should().ContainKey(key, $"item should contain key '{key}' {because}");
        item[key].L.Should().NotBeNull($"attribute '{key}' should be a list {because}");
        item[key].L.Count.Should().Be(expectedCount, 
            $"attribute '{key}' list should have {expectedCount} elements {because}");
    }
    
    /// <summary>
    /// Asserts that an AttributeValue dictionary contains a specific key with a map.
    /// </summary>
    /// <param name="item">The AttributeValue dictionary.</param>
    /// <param name="key">The key to check for.</param>
    /// <param name="expectedKeys">The expected keys in the map.</param>
    /// <param name="because">Optional reason for the assertion.</param>
    public static void ShouldContainMap(
        this Dictionary<string, AttributeValue> item,
        string key,
        IEnumerable<string> expectedKeys,
        string because = "")
    {
        item.Should().ContainKey(key, $"item should contain key '{key}' {because}");
        item[key].M.Should().NotBeNull($"attribute '{key}' should be a map {because}");
        item[key].M.Keys.Should().BeEquivalentTo(expectedKeys, 
            $"attribute '{key}' map should have expected keys {because}");
    }
    
    /// <summary>
    /// Asserts that an AttributeValue dictionary does not contain a specific key.
    /// Useful for verifying that null or empty collections are omitted.
    /// </summary>
    /// <param name="item">The AttributeValue dictionary.</param>
    /// <param name="key">The key that should not be present.</param>
    /// <param name="because">Optional reason for the assertion.</param>
    public static void ShouldNotContainKey(
        this Dictionary<string, AttributeValue> item,
        string key,
        string because = "")
    {
        item.Should().NotContainKey(key, 
            $"item should not contain key '{key}' {because}");
    }
}
