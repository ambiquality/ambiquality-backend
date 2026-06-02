using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Tests.Domain.Users;

public class UserThrottleTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private static readonly LoginThrottlePolicy Policy = new(
        FreeAttempts: 5,
        BaseDelay: TimeSpan.FromSeconds(1),
        MaxDelay: TimeSpan.FromSeconds(30),
        ResetWindow: TimeSpan.FromMinutes(15));

    private static User NewUser() => User.Register(
        Email.Create("user@example.com"), "hash", "confirm-hash", Now, TimeSpan.FromHours(24));

    private static void FailTimes(User user, int times, DateTime at)
    {
        for (var i = 0; i < times; i++)
            user.RegisterFailedLogin(at, Policy.ResetWindow);
    }

    [Fact]
    public void ThrottleDelay_WithNoFailures_IsZero()
    {
        var user = NewUser();

        Assert.Equal(TimeSpan.Zero, user.ThrottleDelay(Now, Policy));
    }

    [Fact]
    public void ThrottleDelay_WithinFreeBudget_IsZero()
    {
        var user = NewUser();
        FailTimes(user, Policy.FreeAttempts - 1, Now); // still inside the free budget

        Assert.Equal(TimeSpan.Zero, user.ThrottleDelay(Now, Policy));
    }

    [Fact]
    public void ThrottleDelay_GrowsExponentiallyOnceBudgetExhausted()
    {
        var user = NewUser();
        FailTimes(user, Policy.FreeAttempts, Now); // budget exhausted -> first delay

        // base * 2^0 = 1s
        Assert.Equal(TimeSpan.FromSeconds(1), user.ThrottleDelay(Now, Policy));

        user.RegisterFailedLogin(Now, Policy.ResetWindow); // -> 2s
        Assert.Equal(TimeSpan.FromSeconds(2), user.ThrottleDelay(Now, Policy));

        user.RegisterFailedLogin(Now, Policy.ResetWindow); // -> 4s
        Assert.Equal(TimeSpan.FromSeconds(4), user.ThrottleDelay(Now, Policy));
    }

    [Fact]
    public void ThrottleDelay_IsCappedAtMaxDelay()
    {
        var user = NewUser();
        FailTimes(user, Policy.FreeAttempts + 20, Now); // far beyond the cap

        Assert.Equal(Policy.MaxDelay, user.ThrottleDelay(Now, Policy));
    }

    [Fact]
    public void ThrottleDelay_AfterColdStreak_IsZero()
    {
        var user = NewUser();
        FailTimes(user, Policy.FreeAttempts + 3, Now);

        // Asking after the reset window has elapsed: the streak is cold.
        var later = Now + Policy.ResetWindow + TimeSpan.FromMinutes(1);
        Assert.Equal(TimeSpan.Zero, user.ThrottleDelay(later, Policy));
    }

    [Fact]
    public void RegisterFailedLogin_AfterColdStreak_RestartsCount()
    {
        var user = NewUser();
        FailTimes(user, 4, Now);
        Assert.Equal(4, user.FailedLoginCount);

        var later = Now + Policy.ResetWindow + TimeSpan.FromMinutes(1);
        user.RegisterFailedLogin(later, Policy.ResetWindow);

        Assert.Equal(1, user.FailedLoginCount);
    }

    [Fact]
    public void RegisterSuccessfulLogin_ClearsStreak()
    {
        var user = NewUser();
        FailTimes(user, Policy.FreeAttempts + 2, Now);

        user.RegisterSuccessfulLogin();

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LastFailedLoginAt);
        Assert.Equal(TimeSpan.Zero, user.ThrottleDelay(Now, Policy));
    }
}
