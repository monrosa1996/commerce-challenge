using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Commerce.Application.Audit;
using Microsoft.IdentityModel.Tokens;

namespace Commerce.Tests;

public sealed class ApiEndpointTests
{
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.Clone();
    }

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", fileName);

        return form;
    }

    // ---- who can come in -------------------------------------------------------------------

    [Fact]
    public async Task Health_is_open_without_a_session()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/commerce")]
    [InlineData("/api/commerce/quarantine")]
    public async Task Queries_need_a_session(string url)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Debe iniciar sesión para continuar.", (await ReadJsonAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Upload_and_process_need_a_session()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var upload = await client.PostAsync("/api/commerce/files", FileContent([1], "commerce_15032024.csv"));
        var process = await client.PostAsJsonAsync("/api/commerce/process", new { processDate = "2024-03-15" });

        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, process.StatusCode);
        Assert.Empty(factory.Commerce.CreatedRows);
    }

    [Fact]
    public async Task A_made_up_token_is_refused()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        var response = await client.GetAsync("/api/commerce");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Su sesión ha expirado o no es válida. Inicie sesión de nuevo.", (await ReadJsonAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_refused()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var forged = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "commerce-api",
            audience: "commerce-web",
            claims: [new Claim("sub", "1"), new Claim("unique_name", "admin"), new Claim("jti", "forged")],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-key-that-is-long-enough-for-hs256-0123456789")),
                SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await client.GetAsync("/api/commerce");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        // The token is issued with a clock two hours in the past, so it expired 90 minutes ago
        var clock = new FixedClock(DateTimeOffset.UtcNow.AddHours(-2));
        using var factory = new ApiFactory(clock);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.LoginAsync(client));

        var response = await client.GetAsync("/api/commerce");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- login and logout --------------------------------------------------------------------

    [Fact]
    public async Task Login_returns_a_token_and_the_session_works()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = TestUsers.Password });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("admin", body.GetProperty("username").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.True(body.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/commerce")).StatusCode);
    }

    [Theory]
    [InlineData("admin", "wrong-password")]
    [InlineData("nobody", "Secret#2026")]
    [InlineData("retired", "Secret#2026")]
    public async Task Login_with_bad_credentials_gets_the_same_clear_message(string username, string password)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Usuario o contraseña incorrectos.", (await ReadJsonAsync(response)).GetProperty("detail").GetString());

        var entry = Assert.Single(factory.Audit.Entries);
        Assert.Equal(AuditActions.LoginFailed, entry.Action);
        Assert.Equal(401, entry.StatusCode);
        Assert.DoesNotContain(password, entry.RequestPayload + entry.ResponsePayload);
    }

    [Fact]
    public async Task Login_without_data_is_a_400()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Ingrese usuario y contraseña.", (await ReadJsonAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Logout_closes_the_session_and_the_token_stops_working()
    {
        using var factory = new ApiFactory();
        using var client = await factory.CreateLoggedInClientAsync();

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { reason = "manual" });
        var after = await client.GetAsync("/api/commerce");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal(["Login", "Logout"], factory.Audit.Entries.Select(e => e.Action).ToArray());
    }

    [Fact]
    public async Task Logout_accepts_an_empty_body()
    {
        using var factory = new ApiFactory();
        using var client = await factory.CreateLoggedInClientAsync();

        var logout = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task Closing_and_resume_keep_the_session_open_and_audit_nothing()
    {
        using var factory = new ApiFactory();
        using var client = await factory.CreateLoggedInClientAsync();

        var closing = await client.PostAsync("/api/auth/closing", null);
        var resume = await client.PostAsync("/api/auth/resume", null);
        var after = await client.GetAsync("/api/commerce");

        Assert.Equal(HttpStatusCode.NoContent, closing.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, resume.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal(["Login"], factory.Audit.Entries.Select(e => e.Action).ToArray());
    }

    [Fact]
    public async Task Closing_and_resume_need_a_session()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/closing", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/resume", null)).StatusCode);
    }

    [Fact]
    public async Task Logout_needs_a_session()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- what is audited -----------------------------------------------------------------------

    [Fact]
    public async Task Empty_file_returns_400_and_is_audited_with_who_what_and_the_answer()
    {
        using var factory = new ApiFactory();
        using var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync("/api/commerce/files", FileContent([], "commerce_15032024.csv"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("El archivo está vacío", (await ReadJsonAsync(response)).GetProperty("detail").GetString());

        var entry = factory.Audit.Entries.Single(e => e.Action == AuditActions.Upload);
        Assert.Equal(400, entry.StatusCode);
        Assert.Equal("admin", entry.Username);
        Assert.Equal(1, entry.UserId);
        Assert.Contains("commerce_15032024.csv", entry.RequestPayload);
        Assert.Contains("El archivo está vacío", entry.ResponsePayload);
    }

    [Fact]
    public async Task A_valid_upload_is_saved_and_audited()
    {
        using var factory = new ApiFactory();
        using var client = await factory.CreateLoggedInClientAsync();
        var bytes = Samples.Read("valid-comma", "commerce_15032024.csv");

        var response = await client.PostAsync("/api/commerce/files", FileContent(bytes, "commerce_15032024.csv"));
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(10, body.GetProperty("insertedCount").GetInt32());
        Assert.Equal(10, factory.Commerce.CreatedRows.Count);

        var entry = factory.Audit.Entries.Single(e => e.Action == AuditActions.Upload);
        Assert.Equal(201, entry.StatusCode);
        Assert.Contains($"\"sizeBytes\":{bytes.Length}", entry.RequestPayload);
        Assert.Contains("\"insertedCount\":10", entry.ResponsePayload);
    }

    [Fact]
    public async Task Process_returns_the_count_and_is_audited()
    {
        using var factory = new ApiFactory();
        using var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/commerce/process", new { processDate = "2024-03-15" });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, body.GetProperty("quarantinedCount").GetInt32());

        var entry = factory.Audit.Entries.Single(e => e.Action == AuditActions.Process);
        Assert.Equal(200, entry.StatusCode);
        Assert.Contains("2024-03-15", entry.RequestPayload);
        Assert.Contains("\"quarantinedCount\":5", entry.ResponsePayload);
    }

    [Fact]
    public async Task Process_without_date_returns_400_and_is_audited()
    {
        using var factory = new ApiFactory();
        using var client = await factory.CreateLoggedInClientAsync();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/commerce/process", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, factory.Audit.Entries.Single(e => e.Action == AuditActions.Process).StatusCode);
    }

    [Fact]
    public async Task Queries_are_not_audited()
    {
        using var factory = new ApiFactory();
        using var client = await factory.CreateLoggedInClientAsync();

        await client.GetAsync("/api/commerce");
        await client.GetAsync("/api/commerce/quarantine");

        Assert.Equal(["Login"], factory.Audit.Entries.Select(e => e.Action).ToArray());
    }

    [Fact]
    public async Task List_with_a_bad_date_returns_400()
    {
        using var factory = new ApiFactory();
        using var client = await factory.CreateLoggedInClientAsync();

        var response = await client.GetAsync("/api/commerce?processDate=15/03/2024");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
