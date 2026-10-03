using Commerce.Application.Abstractions;
using Commerce.Application.Audit;
using Commerce.Application.Auth;
using Commerce.Application.Models;

namespace Commerce.Tests;

internal sealed class FakeAuditRepository : IAuditRepository
{
    public List<AuditEntry> Entries { get; } = [];

    public Task TryAddAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        lock (Entries)
        {
            Entries.Add(entry);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeUserRepository(params UserRecord[] users) : IUserRepository
{
    public Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));
}

internal sealed class FakeTokenIssuer(DateTimeOffset expiresAt) : ITokenIssuer
{
    public IssuedToken Issue(UserRecord user) => new($"token-of-{user.Username}", expiresAt, $"jti-of-{user.Username}");
}

internal sealed class FakeCommerceRepository : ICommerceRepository
{
    public int CreateCalls { get; private set; }

    public IReadOnlyList<CommerceRow> CreatedRows { get; private set; } = [];

    public DateOnly? ProcessedDate { get; private set; }

    /// <summary>When set, CreateAsync and ProcessAsync throw it.</summary>
    public Exception? Failure { get; set; }

    public Task<int> CreateAsync(string sourceFile, IReadOnlyList<CommerceRow> rows, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        CreateCalls++;
        CreatedRows = rows;
        return Task.FromResult(rows.Count);
    }

    public Task<int> ProcessAsync(DateOnly processDate, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        ProcessedDate = processDate;
        return Task.FromResult(5);
    }

    public Task<IReadOnlyList<CommerceRecord>> ListAsync(DateOnly? processDate, CancellationToken cancellationToken)
    {
        ProcessedDate = processDate;
        return Task.FromResult<IReadOnlyList<CommerceRecord>>([]);
    }

    public Task<IReadOnlyList<QuarantineRecord>> ListQuarantineAsync(DateOnly? processDate, CancellationToken cancellationToken)
    {
        ProcessedDate = processDate;
        return Task.FromResult<IReadOnlyList<QuarantineRecord>>([]);
    }
}

internal static class TestUsers
{
    public const string Password = "Secret#2026";

    /// <summary>A user whose password is "Secret#2026". Few iterations, so the tests are fast.</summary>
    public static UserRecord Create(string username = "admin", bool active = true, int id = 1) =>
        new(id, username, $"Usuario {username}", PasswordHasher.Hash(Password, iterations: 1000), active);
}
