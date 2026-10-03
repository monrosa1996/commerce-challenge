using Commerce.Application.Auth;

namespace Commerce.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_has_the_expected_format_and_never_contains_the_password()
    {
        var hash = PasswordHasher.Hash("Secret#2026", iterations: 1000);

        var parts = hash.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2-SHA256", parts[0]);
        Assert.Equal("1000", parts[1]);
        Assert.DoesNotContain("Secret", hash);
    }

    [Fact]
    public void Verify_accepts_the_right_password_and_rejects_a_wrong_one()
    {
        var hash = PasswordHasher.Hash("Secret#2026", iterations: 1000);

        Assert.True(PasswordHasher.Verify("Secret#2026", hash));
        Assert.False(PasswordHasher.Verify("secret#2026", hash));
        Assert.False(PasswordHasher.Verify(string.Empty, hash));
    }

    [Fact]
    public void The_same_password_gives_a_different_hash_each_time()
    {
        Assert.NotEqual(PasswordHasher.Hash("Secret#2026", 1000), PasswordHasher.Hash("Secret#2026", 1000));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("PBKDF2-SHA256$abc$salt$hash")]
    [InlineData("PBKDF2-SHA256$1000$not base64!$not base64!")]
    [InlineData("OTHER$1000$AAAA$AAAA")]
    public void Verify_rejects_a_value_that_is_not_a_hash(string stored)
    {
        Assert.False(PasswordHasher.Verify("Secret#2026", stored));
    }

    [Fact]
    public void A_hash_made_outside_dotnet_with_the_same_algorithm_is_accepted()
    {
        // Made with Python (hashlib.pbkdf2_hmac), the same way the development users of the schema were made
        const string stored = "PBKDF2-SHA256$1000$AAECAwQFBgcICQoLDA0ODw==$rQlPG5vG58lCGVsPFyGy9zKCy8YUKqTUbFpkKX87IB4=";

        Assert.True(PasswordHasher.Verify("Secret#2026", stored));
        Assert.False(PasswordHasher.Verify("Secret#2027", stored));
    }
}
