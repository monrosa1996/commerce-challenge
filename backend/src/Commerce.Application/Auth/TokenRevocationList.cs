using System.Collections.Concurrent;

namespace Commerce.Application.Auth;

/// <summary>In memory. A restart empties it, which is fine because tokens expire in minutes.</summary>
public sealed class TokenRevocationList(TimeProvider clock) : ITokenRevocation
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> revoked = new(StringComparer.Ordinal);

    public void Revoke(string jti, DateTimeOffset expiresAt)
    {
        Purge();
        revoked[jti] = expiresAt;
    }

    public bool IsRevoked(string jti) => revoked.ContainsKey(jti);

    // A token that already expired is rejected anyway, so it does not need to stay in the list
    private void Purge()
    {
        var now = clock.GetUtcNow();

        foreach (var (jti, expiresAt) in revoked)
        {
            if (expiresAt < now)
            {
                revoked.TryRemove(jti, out _);
            }
        }
    }
}
