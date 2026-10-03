using System.Data;
using Commerce.Application.Abstractions;
using Commerce.Application.Errors;
using Commerce.Application.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Commerce.Infrastructure;

public sealed class CommerceRepository(ISqlConnectionFactory connectionFactory) : ICommerceRepository
{
    private const string TableTypeName = "dbo.CommerceTableType";

    // Raised by sp_create_commerce when a key already exists (message lists up to 20 keys)
    private const int ExistingKeysErrorNumber = 50001;
    private const int UniqueIndexErrorNumber = 2601;
    private const int UniqueConstraintErrorNumber = 2627;

    public async Task<int> CreateAsync(string sourceFile, IReadOnlyList<CommerceRow> rows, CancellationToken cancellationToken)
    {
        using var table = ToTable(rows);
        await using var connection = connectionFactory.Create();

        var command = new CommandDefinition(
            "dbo.sp_create_commerce",
            new { SourceFile = sourceFile, Rows = table.AsTableValuedParameter(TableTypeName) },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        try
        {
            return await connection.ExecuteScalarAsync<int>(command);
        }
        catch (SqlException ex) when (ex.Number == ExistingKeysErrorNumber)
        {
            throw new ConflictException(ex.Message);
        }
        catch (SqlException ex) when (ex.Number is UniqueIndexErrorNumber or UniqueConstraintErrorNumber)
        {
            // Two uploads passed the check at the same time; the unique index stopped the second one
            throw new ConflictException("Hay registros que ya existen.");
        }
    }

    public async Task<int> ProcessAsync(DateOnly processDate, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();

        var parameters = new DynamicParameters();
        parameters.Add("ProcessDate", processDate.ToDateTime(TimeOnly.MinValue), DbType.Date);

        var command = new CommandDefinition(
            "dbo.sp_process_commerce",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<IReadOnlyList<CommerceRecord>> ListAsync(DateOnly? processDate, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();

        var parameters = new DynamicParameters();
        parameters.Add("ProcessDate", processDate?.ToDateTime(TimeOnly.MinValue), DbType.Date);

        var command = new CommandDefinition(
            "dbo.sp_list_commerce",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        var items = await connection.QueryAsync<CommerceListRow>(command);

        return items
            .Select(r => new CommerceRecord(
                r.Id,
                r.SourceFile,
                DateOnly.FromDateTime(r.ProcessDate),
                r.CodComRed,
                r.NomComRed,
                r.TipDoc,
                r.NumDoc,
                DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)))
            .ToList();
    }

    public async Task<IReadOnlyList<QuarantineRecord>> ListQuarantineAsync(DateOnly? processDate, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();

        var parameters = new DynamicParameters();
        parameters.Add("ProcessDate", processDate?.ToDateTime(TimeOnly.MinValue), DbType.Date);

        var command = new CommandDefinition(
            "dbo.sp_list_commerce_quarantine",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        var items = await connection.QueryAsync<QuarantineRow>(command);

        return items
            .Select(r => new QuarantineRecord(
                r.Id,
                r.SourceFile,
                DateOnly.FromDateTime(r.ProcessDate),
                r.CodComRed,
                r.NomComRed,
                r.TipDoc,
                r.NumDoc,
                r.Motivo,
                DateTime.SpecifyKind(r.QuarantinedAt, DateTimeKind.Utc)))
            .ToList();
    }

    // Column order must match dbo.CommerceTableType
    private static DataTable ToTable(IReadOnlyList<CommerceRow> rows)
    {
        var table = new DataTable();
        table.Columns.Add("pc_processdate", typeof(DateTime));
        table.Columns.Add("pc_codcomred", typeof(string));
        table.Columns.Add("pc_nomcomred", typeof(string));
        table.Columns.Add("pc_tipdoc", typeof(string));
        table.Columns.Add("pc_numdoc", typeof(string));

        foreach (var row in rows)
        {
            table.Rows.Add(
                row.ProcessDate.ToDateTime(TimeOnly.MinValue),
                row.CodComRed,
                (object?)row.NomComRed ?? DBNull.Value,
                (object?)row.TipDoc ?? DBNull.Value,
                (object?)row.NumDoc ?? DBNull.Value);
        }

        return table;
    }
}

internal sealed class CommerceListRow
{
    public long Id { get; set; }

    public string SourceFile { get; set; } = string.Empty;

    public DateTime ProcessDate { get; set; }

    public string CodComRed { get; set; } = string.Empty;

    public string? NomComRed { get; set; }

    public string? TipDoc { get; set; }

    public string? NumDoc { get; set; }

    public DateTime CreatedAt { get; set; }
}

internal sealed class QuarantineRow
{
    public long Id { get; set; }

    public string SourceFile { get; set; } = string.Empty;

    public DateTime ProcessDate { get; set; }

    public string CodComRed { get; set; } = string.Empty;

    public string? NomComRed { get; set; }

    public string? TipDoc { get; set; }

    public string? NumDoc { get; set; }

    public string Motivo { get; set; } = string.Empty;

    public DateTime QuarantinedAt { get; set; }
}
