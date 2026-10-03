using System.Globalization;
using Commerce.Api.Auth;
using Commerce.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, SessionClosing closing, TimeProvider clock) : ControllerBase
{
    /// <summary>Checks the user and the password and returns a token. The attempt is saved in the audit table.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Ok(result);
    }

    /// <summary>Closes the session: the token stops working and the logout is saved in the audit table.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        var (jti, expires) = TokenIdentity();

        closing.Resume(jti);
        await auth.LogoutAsync(this.ToAuditContext(), jti, expires, request?.Reason);

        return NoContent();
    }

    /// <summary>The page is leaving (close or reload). The session closes in a few seconds unless the page calls resume.</summary>
    [HttpPost("closing")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public IActionResult Closing()
    {
        var (jti, expires) = TokenIdentity();
        closing.Announce(this.ToAuditContext(), jti, expires);

        return NoContent();
    }

    /// <summary>The page came back after a reload: cancels the pending close. Fails with 401 if the session was already closed.</summary>
    [HttpPost("resume")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public IActionResult Resume()
    {
        closing.Resume(TokenIdentity().Jti);

        return NoContent();
    }

    private (string Jti, DateTimeOffset Expires) TokenIdentity()
    {
        var jti = User.ClaimValue(TokenClaims.Jti) ?? string.Empty;
        var expires = long.TryParse(User.ClaimValue(TokenClaims.Expires), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : clock.GetUtcNow().AddHours(1);

        return (jti, expires);
    }
}
