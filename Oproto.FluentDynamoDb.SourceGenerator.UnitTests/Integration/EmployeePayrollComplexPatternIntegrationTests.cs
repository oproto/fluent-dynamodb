using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests verifying the real-world employee payroll pattern scenario.
/// Tests 4 entities: Employee (EMPLOYEE#*), PayRate (EMPLOYEE#*#PAYRATE#*),
/// Deduction (EMPLOYEE#*#DEDUCTION#*), Garnishment (EMPLOYEE#*#GARNISHMENT#*).
///
/// Requirements: 6.1, 6.2, 6.3
/// </summary>
public class EmployeePayrollComplexPatternIntegrationTests
{
    /// <summary>
    /// PayRate, Deduction, and Garnishment are all Complex patterns with score 2
    /// and different distinguishing segments. No DISC004 diagnostics should be
    /// emitted between any pair of these three sibling patterns.
    /// Employee (StartsWith, score 1) overlaps with all three Complex patterns,
    /// producing 3 DISC005 diagnostics (resolved by specificity).
    /// Employee entity receives 3 exclusion guards (one for each child).
    /// </summary>
    [Fact]
    public void Analyze_EmployeePayrollPattern_NoDISC004_ThreeDISC005_ThreeExclusionGuards()
    {
        // Arrange
        var employeeEntity = new EntityModel
        {
            ClassName = "Employee",
            Namespace = "TestNamespace",
            TableName = "employee-table",
            TableEntityCount = 4,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "EMPLOYEE#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var payRateEntity = new EntityModel
        {
            ClassName = "PayRate",
            Namespace = "TestNamespace",
            TableName = "employee-table",
            TableEntityCount = 4,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "EMPLOYEE#*#PAYRATE#*",
                Strategy = DiscriminatorStrategy.Complex,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var deductionEntity = new EntityModel
        {
            ClassName = "Deduction",
            Namespace = "TestNamespace",
            TableName = "employee-table",
            TableEntityCount = 4,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "EMPLOYEE#*#DEDUCTION#*",
                Strategy = DiscriminatorStrategy.Complex,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var garnishmentEntity = new EntityModel
        {
            ClassName = "Garnishment",
            Namespace = "TestNamespace",
            TableName = "employee-table",
            TableEntityCount = 4,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "EMPLOYEE#*#GARNISHMENT#*",
                Strategy = DiscriminatorStrategy.Complex,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var tableEntities = new List<EntityModel>
        {
            employeeEntity, payRateEntity, deductionEntity, garnishmentEntity
        };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — zero DISC004 between the three Complex sibling patterns
        diagnostics.Where(d => d.Id == "DISC004").Should().BeEmpty(
            "PayRate, Deduction, and Garnishment have different distinguishing segments " +
            "and should not produce ambiguous overlap errors");

        // Assert — 3 DISC005 diagnostics: Employee overlaps with each of the three children
        diagnostics.Where(d => d.Id == "DISC005").Should().HaveCount(3,
            "Employee (StartsWith, score 1) overlaps with each Complex child (score 2), " +
            "producing one DISC005 per resolved overlap");

        // Assert — Employee entity gets 3 exclusion patterns (one for each child)
        employeeEntity.Discriminator.OverlappingPatterns.Should().HaveCount(3,
            "Employee should have exclusion guards for PayRate, Deduction, and Garnishment");

        // Verify the exclusion patterns reference the correct child entities
        var exclusionEntityNames = employeeEntity.Discriminator.OverlappingPatterns
            .Select(ep => ep.EntityName)
            .ToList();
        exclusionEntityNames.Should().Contain("PayRate");
        exclusionEntityNames.Should().Contain("Deduction");
        exclusionEntityNames.Should().Contain("Garnishment");

        // Verify child entities do NOT receive exclusion patterns
        payRateEntity.Discriminator.OverlappingPatterns.Should().BeEmpty();
        deductionEntity.Discriminator.OverlappingPatterns.Should().BeEmpty();
        garnishmentEntity.Discriminator.OverlappingPatterns.Should().BeEmpty();
    }
}
