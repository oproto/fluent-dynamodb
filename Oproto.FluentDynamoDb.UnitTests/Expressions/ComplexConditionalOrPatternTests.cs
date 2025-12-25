using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for complex conditional OR patterns where a skip flag is combined with
/// mutually exclusive entity conditions using OR.
/// 
/// Pattern: skipFlag || (condA && entityFilter1) || (condB && entityFilter2)
/// 
/// This pattern is used when:
/// - skipFlag = true: Skip the entire filter clause
/// - skipFlag = false: Apply one of the mutually exclusive conditions based on condA/condB
/// </summary>
public class ComplexConditionalOrPatternTests
{
    private class TenantEntity
    {
        public string Gsi1Pk { get; set; } = string.Empty;
        public bool IsSuspendedByPlatform { get; set; }
        public DateTime? ScheduledDeletionDate { get; set; }
        public bool IsUnderLegalHold { get; set; }
        public string TenantName { get; set; } = string.Empty;
    }

    private ExpressionTranslator CreateTranslator() => new();

    private ExpressionContext CreateContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            null,
            ExpressionValidationMode.None);
    }

    /// <summary>
    /// Tests that when ALL simple OR skip patterns evaluate to skip, the result is empty.
    /// Pattern: (skip1 || cond1) && (skip2 || cond2) && (skip3 || cond3)
    /// </summary>
    [Fact]
    public void Translate_AllSimpleOrPatternsSkip_ShouldReturnEmpty()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var skipSuspendedFilter = true;
        var skipLegalHoldFilter = true;
        var skipNamePrefixFilter = true;
        
        var isSuspendedFilter = false;
        var isUnderLegalHoldFilter = false;
        string? namePrefix = null;

        Expression<Func<TenantEntity, bool>> expression = x =>
            (skipSuspendedFilter || x.IsSuspendedByPlatform == isSuspendedFilter) &&
            (skipLegalHoldFilter || x.IsUnderLegalHold == isUnderLegalHoldFilter) &&
            (skipNamePrefixFilter || x.TenantName.StartsWith(namePrefix!));

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that when ONE simple OR pattern applies, only that filter is returned.
    /// </summary>
    [Fact]
    public void Translate_OneSimpleOrPatternApplies_ShouldReturnThatFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var skipSuspendedFilter = false;  // This one applies
        var skipLegalHoldFilter = true;
        var skipNamePrefixFilter = true;
        
        var isSuspendedFilter = true;
        var isUnderLegalHoldFilter = false;
        string? namePrefix = null;

        Expression<Func<TenantEntity, bool>> expression = x =>
            (skipSuspendedFilter || x.IsSuspendedByPlatform == isSuspendedFilter) &&
            (skipLegalHoldFilter || x.IsUnderLegalHold == isUnderLegalHoldFilter) &&
            (skipNamePrefixFilter || x.TenantName.StartsWith(namePrefix!));

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("IsSuspendedByPlatform");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeTrue();
    }

    /// <summary>
    /// Tests that when MULTIPLE simple OR patterns apply, they are combined with AND.
    /// </summary>
    [Fact]
    public void Translate_MultipleSimpleOrPatternsApply_ShouldCombineWithAnd()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var skipSuspendedFilter = false;  // This one applies
        var skipLegalHoldFilter = false;  // This one applies
        var skipNamePrefixFilter = true;
        
        var isSuspendedFilter = true;
        var isUnderLegalHoldFilter = false;
        string? namePrefix = null;

        Expression<Func<TenantEntity, bool>> expression = x =>
            (skipSuspendedFilter || x.IsSuspendedByPlatform == isSuspendedFilter) &&
            (skipLegalHoldFilter || x.IsUnderLegalHold == isUnderLegalHoldFilter) &&
            (skipNamePrefixFilter || x.TenantName.StartsWith(namePrefix!));

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1 = :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("IsSuspendedByPlatform");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("IsUnderLegalHold");
    }
    
    /// <summary>
    /// Tests the complex deletion filter pattern when skipped.
    /// Pattern: skipFlag || (condA && entityFilter1) || (condB && entityFilter2)
    /// When skipFlag is true, the entire clause should skip regardless of condA/condB.
    /// </summary>
    [Fact]
    public void Translate_ComplexOrPattern_WhenSkipped_ShouldReturnEmpty()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var skipDeletionFilter = true;  // Skip the entire deletion filter
        var hasPendingDeletion = false; // This shouldn't matter since we're skipping

        Expression<Func<TenantEntity, bool>> expression = x =>
            skipDeletionFilter || 
            (hasPendingDeletion && x.ScheduledDeletionDate != null) || 
            (!hasPendingDeletion && x.ScheduledDeletionDate == null);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
    }
    
    /// <summary>
    /// Tests the complex deletion filter pattern when NOT skipped and hasPendingDeletion is true.
    /// Should return the first entity condition.
    /// </summary>
    [Fact]
    public void Translate_ComplexOrPattern_WhenApplied_FirstConditionTrue_ShouldReturnFirstFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var skipDeletionFilter = false;  // Apply the deletion filter
        var hasPendingDeletion = true;   // First condition is true

        Expression<Func<TenantEntity, bool>> expression = x =>
            skipDeletionFilter || 
            (hasPendingDeletion && x.ScheduledDeletionDate != null) || 
            (!hasPendingDeletion && x.ScheduledDeletionDate == null);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().NotBeEmpty();
        context.AttributeNames.AttributeNames.Should().ContainValue("ScheduledDeletionDate");
    }
    
    /// <summary>
    /// Tests the complex deletion filter pattern when NOT skipped and hasPendingDeletion is false.
    /// Should return the second entity condition.
    /// </summary>
    [Fact]
    public void Translate_ComplexOrPattern_WhenApplied_SecondConditionTrue_ShouldReturnSecondFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var skipDeletionFilter = false;  // Apply the deletion filter
        var hasPendingDeletion = false;  // Second condition is true

        Expression<Func<TenantEntity, bool>> expression = x =>
            skipDeletionFilter || 
            (hasPendingDeletion && x.ScheduledDeletionDate != null) || 
            (!hasPendingDeletion && x.ScheduledDeletionDate == null);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().NotBeEmpty();
        context.AttributeNames.AttributeNames.Should().ContainValue("ScheduledDeletionDate");
    }
}
