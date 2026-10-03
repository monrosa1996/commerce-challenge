using System.Collections.Concurrent;
using Commerce.Application.Audit;

namespace Commerce.Application.Auth;

/// <summary>
/// A page that is leaving cannot tell the server whether it is a reload or a real close. The page says
/// "I am leaving"; if it does not come back within the grace period, the session is closed and audited.
/// </summary>
public sealed class SessionClosing(AuthService auth, TimeProvider clock)
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(15);

    private sealed record Pending(AuditContext Context, DateTimeOffset ExpiresAt, DateTimeOffset Deadline);

    private readonly ConcurrentDictionary<string, Pending> pending = new(StringComparer.Ordinal);

    /// <summary>The page is leaving: the session closes when the grace period ends, unless the page comes back.</summary>
    public void Announce(AuditContext context, string jti, DateTimeOffset expiresAt) =>
        pending[jti] = new Pending(context, expiresAt, clock.GetUtcNow().Add(GracePeriod));

    /// <summary>The page came back (reload): the session goes on.</summary>
    public void Resume(string jti) => pending.TryRemove(jti, out _);

    /// <summary>Closes and audits the sessions whose grace period ended. Returns how many were closed.</summary>
    public async Task<int> CloseDueAsync()
    {
        var now = clock.GetUtcNow();
        var closed = 0;

        foreach (var (jti, entry) in pending)
        {
            if (entry.Deadline > now || !pending.TryRemove(jti, out _))
            {
                continue;
            }

            await auth.LogoutAsync(entry.Context, jti, entry.ExpiresAt, "pagehide");
            closed++;
        }

        return closed;
    }
}
