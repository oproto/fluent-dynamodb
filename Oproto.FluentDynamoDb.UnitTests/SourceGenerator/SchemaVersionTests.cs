using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Unit tests for the SchemaVersion value object.
/// Validates: Requirements 4.1, 5.1
/// </summary>
public class SchemaVersionTests
{
    #region CompareTo Ordering

    [Fact]
    public void CompareTo_SameMajorLowerMinor_IsLessThan()
    {
        var v10 = new SchemaVersion(1, 0);
        var v11 = new SchemaVersion(1, 1);

        v10.CompareTo(v11).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_LowerMajor_IsLessThanHigherMajor()
    {
        var v11 = new SchemaVersion(1, 1);
        var v20 = new SchemaVersion(2, 0);

        v11.CompareTo(v20).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_TransitiveOrdering()
    {
        var v10 = new SchemaVersion(1, 0);
        var v11 = new SchemaVersion(1, 1);
        var v20 = new SchemaVersion(2, 0);

        v10.CompareTo(v11).Should().BeNegative();
        v11.CompareTo(v20).Should().BeNegative();
        v10.CompareTo(v20).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_EqualVersions_ReturnsZero()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 0);

        v1.CompareTo(v2).Should().Be(0);
    }

    [Fact]
    public void CompareTo_HigherVersion_IsPositive()
    {
        var v20 = new SchemaVersion(2, 0);
        var v11 = new SchemaVersion(1, 1);

        v20.CompareTo(v11).Should().BePositive();
    }

    #endregion

    #region Equals

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 0);

        v1.Equals(v2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentMajor_ReturnsFalse()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(2, 0);

        v1.Equals(v2).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentMinor_ReturnsFalse()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 1);

        v1.Equals(v2).Should().BeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_SameValues_ReturnsTrue()
    {
        var v1 = new SchemaVersion(1, 0);
        object v2 = new SchemaVersion(1, 0);

        v1.Equals(v2).Should().BeTrue();
    }

    [Fact]
    public void Equals_ObjectOverload_DifferentType_ReturnsFalse()
    {
        var v1 = new SchemaVersion(1, 0);
        object other = "not a version";

        v1.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_Null_ReturnsFalse()
    {
        var v1 = new SchemaVersion(1, 0);

        v1.Equals(null).Should().BeFalse();
    }

    #endregion

    #region GetHashCode

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 0);

        v1.GetHashCode().Should().Be(v2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHash()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(2, 0);

        v1.GetHashCode().Should().NotBe(v2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ConsistentWithEquals()
    {
        var v1 = new SchemaVersion(3, 5);
        var v2 = new SchemaVersion(3, 5);

        v1.Equals(v2).Should().BeTrue();
        v1.GetHashCode().Should().Be(v2.GetHashCode());
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ProducesMajorDotMinor()
    {
        var v = new SchemaVersion(1, 0);

        v.ToString().Should().Be("1.0");
    }

    [Fact]
    public void ToString_MultiDigitValues()
    {
        var v = new SchemaVersion(12, 34);

        v.ToString().Should().Be("12.34");
    }

    #endregion

    #region Operator Overloads

    [Fact]
    public void LessThan_LowerVersion_ReturnsTrue()
    {
        var v10 = new SchemaVersion(1, 0);
        var v11 = new SchemaVersion(1, 1);

        (v10 < v11).Should().BeTrue();
    }

    [Fact]
    public void LessThan_EqualVersion_ReturnsFalse()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 0);

        (v1 < v2).Should().BeFalse();
    }

    [Fact]
    public void LessThan_HigherVersion_ReturnsFalse()
    {
        var v20 = new SchemaVersion(2, 0);
        var v11 = new SchemaVersion(1, 1);

        (v20 < v11).Should().BeFalse();
    }

    [Fact]
    public void GreaterThan_HigherVersion_ReturnsTrue()
    {
        var v20 = new SchemaVersion(2, 0);
        var v11 = new SchemaVersion(1, 1);

        (v20 > v11).Should().BeTrue();
    }

    [Fact]
    public void GreaterThan_EqualVersion_ReturnsFalse()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 0);

        (v1 > v2).Should().BeFalse();
    }

    [Fact]
    public void GreaterThan_LowerVersion_ReturnsFalse()
    {
        var v10 = new SchemaVersion(1, 0);
        var v11 = new SchemaVersion(1, 1);

        (v10 > v11).Should().BeFalse();
    }

    [Fact]
    public void LessThanOrEqual_LowerVersion_ReturnsTrue()
    {
        var v10 = new SchemaVersion(1, 0);
        var v11 = new SchemaVersion(1, 1);

        (v10 <= v11).Should().BeTrue();
    }

    [Fact]
    public void LessThanOrEqual_EqualVersion_ReturnsTrue()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 0);

        (v1 <= v2).Should().BeTrue();
    }

    [Fact]
    public void LessThanOrEqual_HigherVersion_ReturnsFalse()
    {
        var v20 = new SchemaVersion(2, 0);
        var v11 = new SchemaVersion(1, 1);

        (v20 <= v11).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOrEqual_HigherVersion_ReturnsTrue()
    {
        var v20 = new SchemaVersion(2, 0);
        var v11 = new SchemaVersion(1, 1);

        (v20 >= v11).Should().BeTrue();
    }

    [Fact]
    public void GreaterThanOrEqual_EqualVersion_ReturnsTrue()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 0);

        (v1 >= v2).Should().BeTrue();
    }

    [Fact]
    public void GreaterThanOrEqual_LowerVersion_ReturnsFalse()
    {
        var v10 = new SchemaVersion(1, 0);
        var v11 = new SchemaVersion(1, 1);

        (v10 >= v11).Should().BeFalse();
    }

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 0);

        (v1 == v2).Should().BeTrue();
    }

    [Fact]
    public void EqualityOperator_DifferentValues_ReturnsFalse()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 1);

        (v1 == v2).Should().BeFalse();
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ReturnsTrue()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 1);

        (v1 != v2).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_SameValues_ReturnsFalse()
    {
        var v1 = new SchemaVersion(1, 0);
        var v2 = new SchemaVersion(1, 0);

        (v1 != v2).Should().BeFalse();
    }

    #endregion
}
