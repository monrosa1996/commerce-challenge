using System.Text.Encodings.Web;
using System.Text.Json;

namespace Commerce.Application.Audit;

/// <summary>Turns what came in and what went out into JSON and saves it in the audit table.</summary>
public sealed class AuditService(IAuditRepository repository)
{
    private const int MaxUsernameLength = 50;
    private const int MaxIpLength = 45;

    // Relaxed escaping keeps accents readable in the table
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public Task RecordAsync(string action, AuditContext context, int statusCode, object? request, object? response) =>
        repository.TryAddAsync(
            new AuditEntry(
                context.UserId,
                Truncate(context.Username, MaxUsernameLength) ?? string.Empty,
                action,
                statusCode,
                Truncate(context.IpAddress, MaxIpLength),
                Serialize(request),
                Serialize(response)),
            CancellationToken.None);

    private static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
