namespace Commerce.Application.Auth;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAt, string Username, string DisplayName);

public sealed record LogoutRequest(string? Reason);

public sealed record UserRecord(int Id, string Username, string DisplayName, string PasswordHash, bool IsActive);

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt, string Jti);

public interface IUserRepository
{
    Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken);
}

public interface ITokenIssuer
{
    IssuedToken Issue(UserRecord user);
}

/// <summary>Tokens that were closed with a logout. They stop working even before they expire.</summary>
public interface ITokenRevocation
{
    void Revoke(string jti, DateTimeOffset expiresAt);

    bool IsRevoked(string jti);
}
