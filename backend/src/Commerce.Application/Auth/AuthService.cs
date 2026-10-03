using Commerce.Application.Audit;
using Commerce.Application.Errors;

namespace Commerce.Application.Auth;

public sealed class AuthService(IUserRepository users, ITokenIssuer tokens, ITokenRevocation revocation, AuditService audit)
{
    private const string WrongCredentials = "Usuario o contraseña incorrectos.";

    // Checked when the user does not exist, so a wrong user and a wrong password take the same time
    private static readonly Lazy<string> DummyHash = new(() => PasswordHasher.Hash("not-a-real-password"));

    public async Task<LoginResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var username = (request.Username ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        var requestPayload = new { username }; // the password is never saved

        if (username.Length == 0 || password.Length == 0)
        {
            await audit.RecordAsync(
                AuditActions.LoginFailed, new AuditContext(null, username, ipAddress), 400, requestPayload,
                new { success = false, reason = "missing_data" });

            throw new RequestValidationException("Solicitud inválida", "Ingrese usuario y contraseña.");
        }

        var user = await users.FindByUsernameAsync(username, cancellationToken);
        var passwordIsRight = PasswordHasher.Verify(password, user?.PasswordHash ?? DummyHash.Value);

        if (user is null || !passwordIsRight || !user.IsActive)
        {
            var reason = user is null ? "unknown_user" : !passwordIsRight ? "wrong_password" : "inactive_user";

            await audit.RecordAsync(
                AuditActions.LoginFailed, new AuditContext(user?.Id, username, ipAddress), 401, requestPayload,
                new { success = false, reason });

            throw new AuthenticationFailedException(WrongCredentials);
        }

        var issued = tokens.Issue(user);

        await audit.RecordAsync(
            AuditActions.Login, new AuditContext(user.Id, user.Username, ipAddress), 200, requestPayload,
            new { success = true, expiresAt = issued.ExpiresAt });

        return new LoginResult(issued.Token, issued.ExpiresAt, user.Username, user.DisplayName);
    }

    /// <summary>Closes the session: the token stops working and the logout is audited.</summary>
    public async Task LogoutAsync(AuditContext context, string jti, DateTimeOffset expiresAt, string? reason)
    {
        revocation.Revoke(jti, expiresAt);

        // Only two reasons are kept, so the audit never stores free text from the client
        var cleanReason = string.Equals(reason, "pagehide", StringComparison.OrdinalIgnoreCase) ? "pagehide" : "manual";

        await audit.RecordAsync(AuditActions.Logout, context, 200, new { reason = cleanReason }, new { loggedOut = true });
    }
}
