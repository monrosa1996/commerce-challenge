using System.Globalization;
using System.Security.Claims;
using System.Text;
using Commerce.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Commerce.Api.Auth;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : ITokenIssuer
{
    public IssuedToken Issue(UserRecord user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(jwt.ExpiryMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(TokenClaims.Subject, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(TokenClaims.Name, user.Username),
                new Claim(TokenClaims.Jti, jti),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), expires, jti);
    }
}
