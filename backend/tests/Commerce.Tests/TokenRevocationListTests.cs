using Commerce.Application.Auth;

namespace Commerce.Tests;

public sealed class TokenRevocationListTests
{
    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void A_revoked_token_is_reported_and_the_others_are_not()
    {
        var list = new TokenRevocationList(TimeProvider.System);

        list.Revoke("a", DateTimeOffset.UtcNow.AddMinutes(10));

        Assert.True(list.IsRevoked("a"));
        Assert.False(list.IsRevoked("b"));
    }

    [Fact]
    public void Tokens_that_already_expired_are_removed_when_another_one_is_revoked()
    {
        var clock = new FakeClock(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var list = new TokenRevocationList(clock);
        list.Revoke("old", clock.Now.AddMinutes(30));

        clock.Now = clock.Now.AddHours(2);
        list.Revoke("new", clock.Now.AddMinutes(30));

        Assert.False(list.IsRevoked("old"));
        Assert.True(list.IsRevoked("new"));
    }
}
