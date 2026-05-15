using Ambiquality.Auth.Api.Infrastructure.Security;

namespace Ambiquality.Auth.Api.Tests.Infrastructure;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        var clock = new SystemClock();

        var before = DateTime.UtcNow;
        var value = clock.UtcNow;
        var after = DateTime.UtcNow;

        Assert.InRange(value, before, after);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
    }
}
