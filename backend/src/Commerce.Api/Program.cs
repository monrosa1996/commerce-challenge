using System.Text;
using Commerce.Api.Auth;
using Commerce.Api.Middleware;
using Commerce.Application.Abstractions;
using Commerce.Application.Audit;
using Commerce.Application.Auth;
using Commerce.Application.Files;
using Commerce.Application.Services;
using Commerce.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Commerce");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Commerce'.");
}

builder.Services
    .AddOptions<UploadOptions>()
    .BindConfiguration("Upload")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Request limits follow the same configured file size (plus room for the multipart envelope)
var upload = builder.Configuration.GetSection("Upload").Get<UploadOptions>() ?? new UploadOptions();
var requestLimit = upload.MaxFileSizeBytes + 1_048_576;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = requestLimit);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = requestLimit);

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<UploadOptions>>().Value);
builder.Services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));
builder.Services.AddSingleton<ICommerceRepository, CommerceRepository>();
builder.Services.AddSingleton<CsvFileValidator>();
builder.Services.AddSingleton<CommerceService>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IAuditRepository, AuditRepository>();
builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddSingleton<ITokenRevocation, TokenRevocationList>();
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<SessionClosing>();
builder.Services.AddHostedService<PendingLogoutWorker>();

// The signing key has no default: it must come from the configuration (user-secrets or an environment variable)
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration("Jwt")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = TokenClaims.Name,
        };

        bearer.Events = new JwtBearerEvents
        {
            // A token closed with a logout stops working even before it expires
            OnTokenValidated = context =>
            {
                var revocation = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocation>();
                var jti = context.Principal?.FindFirst(TokenClaims.Jti)?.Value;

                if (jti is null || revocation.IsRevoked(jti))
                {
                    context.Fail("The session was closed.");
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();

                var hadToken = context.Request.Headers.ContainsKey("Authorization");
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "No autorizado",
                    Detail = hadToken
                        ? "Su sesión ha expirado o no es válida. Inicie sesión de nuevo."
                        : "Debe iniciar sesión para continuar.",
                };

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted);
            },
        };
    });

// Everything needs a session, except what is marked as anonymous (login, health, API docs)
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST")
    .WithHeaders("Content-Type", "Authorization")));

var app = builder.Build();

app.UseExceptionHandler();

// In Development the API runs on plain http, so the front can call it without a dev certificate
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

// Lets the integration tests start the app
public partial class Program;
