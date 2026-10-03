using System.ComponentModel.DataAnnotations;

namespace Commerce.Api.Auth;

public sealed class JwtOptions
{
    [Required]
    public string Issuer { get; set; } = "commerce-api";

    [Required]
    public string Audience { get; set; } = "commerce-web";

    /// <summary>At least 32 characters (HS256 needs a key of 256 bits). It is required: there is no default.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 30;
}
