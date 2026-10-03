namespace Commerce.Application.Audit;

public static class AuditActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string Upload = "Upload";
    public const string Process = "Process";
}

/// <summary>Who did the action and from where.</summary>
public sealed record AuditContext(int? UserId, string Username, string? IpAddress);

public sealed record AuditEntry(
    int? UserId,
    string Username,
    string Action,
    int StatusCode,
    string? IpAddress,
    string? RequestPayload,
    string? ResponsePayload);

public interface IAuditRepository
{
    /// <summary>Saves the entry. It never throws: a failure to audit is logged and does not stop the user's action.</summary>
    Task TryAddAsync(AuditEntry entry, CancellationToken cancellationToken);
}
