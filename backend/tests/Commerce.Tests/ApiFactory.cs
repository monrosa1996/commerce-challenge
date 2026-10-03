using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Commerce.Application.Abstractions;
using Commerce.Application.Audit;
using Commerce.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Commerce.Tests;

/// <summary>
/// Starts the real API (authentication, filters, controllers) with the database replaced by in-memory fakes.
/// </summary>
internal sealed class ApiFactory(TimeProvider? clock = null) : WebApplicationFactory<Program>
{
    public FakeAuditRepository Audit { get; } = new();

    public FakeCommerceRepository Commerce { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IAuditRepository>();
            services.RemoveAll<ICommerceRepository>();

            services.AddSingleton<IUserRepository>(new FakeUserRepository(
                TestUsers.Create("admin", id: 1),
                TestUsers.Create("retired", active: false, id: 2)));
            services.AddSingleton<IAuditRepository>(Audit);
            services.AddSingleton<ICommerceRepository>(Commerce);

            if (clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            }
        });
    }

    public async Task<string> LoginAsync(HttpClient client, string username = "admin", string password = TestUsers.Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    /// <summary>A client that already logged in as admin.</summary>
    public async Task<HttpClient> CreateLoggedInClientAsync()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client));

        return client;
    }
}
