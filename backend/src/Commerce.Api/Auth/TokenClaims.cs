using System.Globalization;
using System.Security.Claims;
using Commerce.Application.Audit;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Auth;

/// <summary>Names of the claims inside the token.</summary>
public static class TokenClaims
{
    public const string Subject = "sub";
    public const string Name = "unique_name";
    public const string Jti = "jti";
    public const string Expires = "exp";
}

public static class UserContextExtensions
{
    /// <summary>Who is calling and from which address, to save it in the audit table.</summary>
    public static AuditContext ToAuditContext(this ControllerBase controller)
    {
        var user = controller.User;
        var subject = user.FindFirst(TokenClaims.Subject)?.Value;
        int? userId = int.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

        return new AuditContext(userId, user.Identity?.Name ?? string.Empty, controller.HttpContext.Connection.RemoteIpAddress?.ToString());
    }

    public static string? ClaimValue(this ClaimsPrincipal user, string type) => user.FindFirst(type)?.Value;
}
