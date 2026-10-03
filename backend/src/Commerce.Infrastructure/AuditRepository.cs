using System.Data;
using Commerce.Application.Audit;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Commerce.Infrastructure;

public sealed class AuditRepository(ISqlConnectionFactory connectionFactory, ILogger<AuditRepository> logger) : IAuditRepository
{
    public async Task TryAddAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = connectionFactory.Create();

            var command = new CommandDefinition(
                "dbo.sp_audit_add",
                new
                {
                    UserId = entry.UserId,
                    entry.Username,
                    entry.Action,
                    entry.StatusCode,
                    entry.IpAddress,
                    entry.RequestPayload,
                    entry.ResponsePayload,
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The user's action already happened. The log never has the payloads, only what failed.
            logger.LogError(ex, "The audit entry for {Action} could not be saved", entry.Action);
        }
    }
}
