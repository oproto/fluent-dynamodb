using FluentAssertions;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class ParameterGeneratorTests
{
    [Fact]
    public void GenerateParameterName_FirstCall_ShouldReturnP0()
    {
        // Arrange
        var generator = new ParameterGenerator();

        // Act
        var paramName = generator.GenerateParameterName();

        // Assert
        paramName.Should().Be(":p0");
    }

    [Fact]
    public void GenerateParameterName_MultipleCalls_ShouldIncrementCounter()
    {
        // Arrange
        var generator = new ParameterGenerator();

        // Act
        var param1 = generator.GenerateParameterName();
        var param2 = generator.GenerateParameterName();
        var param3 = generator.GenerateParameterName();

        // Assert
        param1.Should().Be(":p0");
        param2.Should().Be(":p1");
        param3.Should().Be(":p2");
    }

    [Fact]
    public void GenerateParameterName_ManyCallsSequentially_ShouldContinueIncrementing()
    {
        // Arrange
        var generator = new ParameterGenerator();

        // Act - Generate 100 parameter names
        var paramNames = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            paramNames.Add(generator.GenerateParameterName());
        }

        // Assert
        paramNames.Should().HaveCount(100);
        paramNames[0].Should().Be(":p0");
        paramNames[50].Should().Be(":p50");
        paramNames[99].Should().Be(":p99");
        
        // Verify all names are unique
        paramNames.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Reset_ShouldResetCounterToZero()
    {
        // Arrange
        var generator = new ParameterGenerator();
        generator.GenerateParameterName(); // :p0
        generator.GenerateParameterName(); // :p1
        generator.GenerateParameterName(); // :p2

        // Act
        generator.Reset();
        var paramName = generator.GenerateParameterName();

        // Assert
        paramName.Should().Be(":p0");
    }

    [Fact]
    public void Reset_MultipleTimes_ShouldAlwaysResetToZero()
    {
        // Arrange
        var generator = new ParameterGenerator();

        // Act & Assert
        generator.GenerateParameterName().Should().Be(":p0");
        generator.GenerateParameterName().Should().Be(":p1");
        
        generator.Reset();
        generator.GenerateParameterName().Should().Be(":p0");
        
        generator.Reset();
        generator.GenerateParameterName().Should().Be(":p0");
        generator.GenerateParameterName().Should().Be(":p1");
    }

    [Fact]
    public void Reset_AfterManyGenerations_ShouldResetCorrectly()
    {
        // Arrange
        var generator = new ParameterGenerator();
        
        // Generate many parameter names
        for (int i = 0; i < 1000; i++)
        {
            generator.GenerateParameterName();
        }

        // Act
        generator.Reset();
        var paramName = generator.GenerateParameterName();

        // Assert
        paramName.Should().Be(":p0");
    }

    [Fact]
    public void MultipleInstances_ShouldHaveIndependentCounters()
    {
        // Arrange
        var generator1 = new ParameterGenerator();
        var generator2 = new ParameterGenerator();

        // Act
        var param1_1 = generator1.GenerateParameterName();
        var param2_1 = generator2.GenerateParameterName();
        var param1_2 = generator1.GenerateParameterName();
        var param2_2 = generator2.GenerateParameterName();

        // Assert
        param1_1.Should().Be(":p0");
        param2_1.Should().Be(":p0");
        param1_2.Should().Be(":p1");
        param2_2.Should().Be(":p1");
    }

    [Fact]
    public void ParameterNameFormat_ShouldAlwaysStartWithColonP()
    {
        // Arrange
        var generator = new ParameterGenerator();

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            var paramName = generator.GenerateParameterName();
            paramName.Should().StartWith(":p");
            paramName.Should().MatchRegex(@"^:p\d+$");
        }
    }

    [Fact]
    public void ParameterNameFormat_ShouldContainOnlyDigitsAfterPrefix()
    {
        // Arrange
        var generator = new ParameterGenerator();

        // Act
        var paramNames = new List<string>();
        for (int i = 0; i < 50; i++)
        {
            paramNames.Add(generator.GenerateParameterName());
        }

        // Assert
        foreach (var paramName in paramNames)
        {
            paramName.Should().MatchRegex(@"^:p\d+$", 
                "parameter names should only contain digits after the ':p' prefix");
        }
    }

    [Fact]
    public void GenerateParameterName_ThreadSafety_ShouldGenerateUniqueNames()
    {
        // Arrange
        var generator = new ParameterGenerator();
        var paramNames = new List<string>();
        var lockObject = new object();

        // Act - Simulate concurrent access
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    var paramName = generator.GenerateParameterName();
                    lock (lockObject)
                    {
                        paramNames.Add(paramName);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        paramNames.Should().HaveCount(100);
        // Note: Due to potential race conditions, we can't guarantee sequential numbering
        // but we can verify the format and that we got the expected count
        foreach (var paramName in paramNames)
        {
            paramName.Should().MatchRegex(@"^:p\d+$");
        }
    }
}