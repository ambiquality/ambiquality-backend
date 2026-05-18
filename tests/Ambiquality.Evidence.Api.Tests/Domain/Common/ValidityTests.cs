using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Tests.Domain.Common;

public class ValidityTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OpenFrom_ProducesRangeWithUpperInfinite()
    {
        var range = Validity.OpenFrom(T0);

        Assert.Equal(T0, range.LowerBound);
        Assert.True(range.UpperBoundInfinite);
        Assert.True(range.LowerBoundIsInclusive);
    }

    [Fact]
    public void OpenFrom_WithNonUtcKind_Throws()
    {
        var local = DateTime.SpecifyKind(T0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => Validity.OpenFrom(local));
    }

    [Fact]
    public void OpenFrom_WithUnspecifiedKind_Throws()
    {
        var unspec = DateTime.SpecifyKind(T0, DateTimeKind.Unspecified);
        Assert.Throws<ArgumentException>(() => Validity.OpenFrom(unspec));
    }

    [Fact]
    public void Closed_ProducesHalfOpenRange()
    {
        var range = Validity.Closed(T0, T1);

        Assert.Equal(T0, range.LowerBound);
        Assert.Equal(T1, range.UpperBound);
        Assert.True(range.LowerBoundIsInclusive);
        Assert.False(range.UpperBoundIsInclusive);
    }

    [Fact]
    public void Closed_WithFromEqualsTo_Throws()
    {
        Assert.Throws<ArgumentException>(() => Validity.Closed(T0, T0));
    }

    [Fact]
    public void Closed_WithFromAfterTo_Throws()
    {
        Assert.Throws<ArgumentException>(() => Validity.Closed(T1, T0));
    }

    [Fact]
    public void Closed_WithNonUtcKind_Throws()
    {
        var local = DateTime.SpecifyKind(T0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => Validity.Closed(local, T1));
        Assert.Throws<ArgumentException>(() => Validity.Closed(T0, DateTime.SpecifyKind(T1, DateTimeKind.Local)));
    }
}
