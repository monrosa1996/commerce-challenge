using Commerce.Application.Audit;
using Commerce.Application.Auth;
using Commerce.Application.Errors;

namespace Commerce.Tests;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Expiry = new(2030, 1, 1, 0, 30, 0, TimeSpan.Zero);

    private static (AuthService Service, FakeAuditRepository Audit, TokenRevocationList Revocation) Create(params Commerce.Application.Auth.UserRecord[] users)
    {
        var audit = new FakeAuditRepository();
        var revocation = new TokenRevocationList(TimeProvider.System);
        var service = new AuthService(new FakeUserRepository(users), new FakeTokenIssuer(Expiry), revocation, new AuditService(audit));

        return (service, audit, revocation);
    }

    [Fact]
    public async Task Login_with_the_right_password_returns_the_token_and_audits_the_login()
    {
        var (service, audit, _) = Create(TestUsers.Create());

        var result = await service.LoginAsync(new LoginRequest("admin", TestUsers.Password), "10.0.0.5", CancellationToken.None);

        Assert.Equal("token-of-admin", result.AccessToken);
        Assert.Equal("admin", result.Username);
        Assert.Equal(Expiry, result.ExpiresAt);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.Login, entry.Action);
        Assert.Equal(200, entry.StatusCode);
        Assert.Equal(1, entry.UserId);
        Assert.Equal("10.0.0.5", entry.IpAddress);
        Assert.Contains("\"username\":\"admin\"", entry.RequestPayload);
        Assert.Contains("\"success\":true", entry.ResponsePayload);
    }

    [Fact]
    public async Task Login_ignores_spaces_and_case_in_the_username()
    {
        var (service, _, _) = Create(TestUsers.Create());

        var result = await service.LoginAsync(new LoginRequest("  ADMIN ", TestUsers.Password), null, CancellationToken.None);

        Assert.Equal("admin", result.Username);
    }

    [Fact]
    public async Task A_wrong_password_is_refused_with_a_clear_message_and_audited()
    {
        var (service, audit, _) = Create(TestUsers.Create());

        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => service.LoginAsync(new LoginRequest("admin", "wrong"), null, CancellationToken.None));

        Assert.Equal("Usuario o contraseña incorrectos.", error.Message);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.LoginFailed, entry.Action);
        Assert.Equal(401, entry.StatusCode);
        Assert.Equal(1, entry.UserId);
        Assert.Contains("wrong_password", entry.ResponsePayload);
    }

    [Fact]
    public async Task An_unknown_user_gets_the_same_message_as_a_wrong_password()
    {
        var (service, audit, _) = Create(TestUsers.Create());

        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => service.LoginAsync(new LoginRequest("nobody", "whatever"), null, CancellationToken.None));

        Assert.Equal("Usuario o contraseña incorrectos.", error.Message);

        var entry = Assert.Single(audit.Entries);
        Assert.Null(entry.UserId);
        Assert.Equal("nobody", entry.Username);
        Assert.Contains("unknown_user", entry.ResponsePayload);
    }

    [Fact]
    public async Task An_inactive_user_cannot_log_in_even_with_the_right_password()
    {
        var (service, audit, _) = Create(TestUsers.Create(active: false));

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => service.LoginAsync(new LoginRequest("admin", TestUsers.Password), null, CancellationToken.None));

        Assert.Contains("inactive_user", audit.Entries[0].ResponsePayload);
    }

    [Theory]
    [InlineData(null, "x")]
    [InlineData("admin", null)]
    [InlineData("  ", "x")]
    [InlineData("admin", "")]
    public async Task Missing_data_is_a_400_and_is_audited(string? username, string? password)
    {
        var (service, audit, _) = Create(TestUsers.Create());

        var error = await Assert.ThrowsAsync<RequestValidationException>(
            () => service.LoginAsync(new LoginRequest(username, password), null, CancellationToken.None));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal(400, Assert.Single(audit.Entries).StatusCode);
    }

    [Fact]
    public async Task The_password_is_never_saved_in_the_audit()
    {
        var (service, audit, _) = Create(TestUsers.Create());

        await service.LoginAsync(new LoginRequest("admin", TestUsers.Password), null, CancellationToken.None);
        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => service.LoginAsync(new LoginRequest("admin", "another-secret"), null, CancellationToken.None));

        foreach (var entry in audit.Entries)
        {
            Assert.DoesNotContain(TestUsers.Password, entry.RequestPayload + entry.ResponsePayload);
            Assert.DoesNotContain("another-secret", entry.RequestPayload + entry.ResponsePayload);
            Assert.DoesNotContain("token-of-admin", entry.RequestPayload + entry.ResponsePayload);
        }
    }

    [Fact]
    public async Task Logout_closes_the_token_and_audits_the_reason()
    {
        var (service, audit, revocation) = Create(TestUsers.Create());

        await service.LogoutAsync(new AuditContext(1, "admin", null), "jti-1", Expiry, "pagehide");

        Assert.True(revocation.IsRevoked("jti-1"));
        Assert.False(revocation.IsRevoked("jti-2"));

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.Logout, entry.Action);
        Assert.Equal(200, entry.StatusCode);
        Assert.Contains("\"reason\":\"pagehide\"", entry.RequestPayload);
        Assert.Contains("\"loggedOut\":true", entry.ResponsePayload);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("manual")]
    [InlineData("anything the client wants")]
    public async Task Logout_only_keeps_the_known_reasons(string? reason)
    {
        var (service, audit, _) = Create(TestUsers.Create());

        await service.LogoutAsync(new AuditContext(1, "admin", null), "jti-1", Expiry, reason);

        Assert.Contains("\"reason\":\"manual\"", audit.Entries[0].RequestPayload);
    }
}
