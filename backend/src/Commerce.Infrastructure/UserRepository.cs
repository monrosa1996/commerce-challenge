using System.Data;
using Commerce.Application.Auth;
using Dapper;

namespace Commerce.Infrastructure;

public sealed class UserRepository(ISqlConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();

        var command = new CommandDefinition(
            "dbo.sp_auth_get_user",
            new { Username = username },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(command);

        return row is null ? null : new UserRecord(row.Id, row.Username, row.DisplayName, row.PasswordHash, row.IsActive);
    }
}

internal sealed class UserRow
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
