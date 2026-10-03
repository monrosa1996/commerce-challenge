using Commerce.Application.Audit;
using Commerce.Application.Auth;

namespace Commerce.Tests;

public sealed class SessionClosingTests
{
    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset Start = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly AuditContext Admin = new(1, "admin", "10.0.0.5");

    private static (SessionClosing Closing, FakeClock Clock, FakeAuditRepository Audit, TokenRevocationList Revocation) Create()
    {
        var clock = new FakeClock(Start);
        var audit = new FakeAuditRepository();
        var revocation = new TokenRevocationList(clock);
        var auth = new AuthService(new FakeUserRepository(), new FakeTokenIssuer(Start.AddMinutes(30)), revocation, new AuditService(audit));

        return (new SessionClosing(auth, clock), clock, audit, revocation);
    }

    [Fact]
    public async Task Nothing_is_closed_while_the_grace_period_lasts()
    {
        var (closing, clock, audit, revocation) = Create();
        closing.Announce(Admin, "jti-1", Start.AddMinutes(30));

        clock.Now = Start.Add(SessionClosing.GracePeriod).AddSeconds(-1);

        Assert.Equal(0, await closing.CloseDueAsync());
        Assert.Empty(audit.Entries);
        Assert.False(revocation.IsRevoked("jti-1"));
    }

    [Fact]
    public async Task A_page_that_does_not_come_back_is_closed_and_audited_once()
    {
        var (closing, clock, audit, revocation) = Create();
        closing.Announce(Admin, "jti-1", Start.AddMinutes(30));

        clock.Now = Start.Add(SessionClosing.GracePeriod).AddSeconds(1);

        Assert.Equal(1, await closing.CloseDueAsync());
        Assert.Equal(0, await closing.CloseDueAsync());

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.Logout, entry.Action);
        Assert.Equal(1, entry.UserId);
        Assert.Contains("\"reason\":\"pagehide\"", entry.RequestPayload);
        Assert.True(revocation.IsRevoked("jti-1"));
    }

    [Fact]
    public async Task A_page_that_comes_back_after_a_reload_keeps_its_session()
    {
        var (closing, clock, audit, revocation) = Create();
        closing.Announce(Admin, "jti-1", Start.AddMinutes(30));
        closing.Resume("jti-1");

        clock.Now = Start.AddMinutes(5);

        Assert.Equal(0, await closing.CloseDueAsync());
        Assert.Empty(audit.Entries);
        Assert.False(revocation.IsRevoked("jti-1"));
    }
}
