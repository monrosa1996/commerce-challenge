using System.Security.Cryptography;
using Commerce.Application.Abstractions;
using Commerce.Application.Audit;
using Commerce.Application.Errors;
using Commerce.Application.Files;
using Commerce.Application.Models;

namespace Commerce.Application.Services;

public sealed class CommerceService(ICommerceRepository repository, CsvFileValidator validator, AuditService audit)
{
    public async Task<UploadResult> UploadAsync(
        string? fileName,
        byte[]? content,
        AuditContext user,
        CancellationToken cancellationToken)
    {
        // What came in: the file is identified by its name, its size and its hash, not saved whole
        var request = new
        {
            fileName,
            sizeBytes = content?.Length ?? 0,
            sha256 = content is { Length: > 0 } ? Convert.ToHexString(SHA256.HashData(content)) : null,
        };

        try
        {
            var file = validator.Validate(fileName, content);
            var inserted = await repository.CreateAsync(file.SourceFile, file.Rows, cancellationToken);
            var result = new UploadResult(file.SourceFile, inserted, file.Warnings);

            await audit.RecordAsync(AuditActions.Upload, user, 201, request, result);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var (status, body) = Describe(ex);
            await audit.RecordAsync(AuditActions.Upload, user, status, request, body);

            throw;
        }
    }

    public async Task<ProcessResult> ProcessAsync(string? processDate, AuditContext user, CancellationToken cancellationToken)
    {
        var request = new { processDate };

        try
        {
            if (string.IsNullOrWhiteSpace(processDate))
            {
                throw new RequestValidationException("Solicitud inválida", "La fecha de proceso es obligatoria.");
            }

            var date = ParseIsoDate(processDate);
            var quarantined = await repository.ProcessAsync(date, cancellationToken);
            var result = new ProcessResult(date, quarantined);

            await audit.RecordAsync(AuditActions.Process, user, 200, request, result);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var (status, body) = Describe(ex);
            await audit.RecordAsync(AuditActions.Process, user, status, request, body);

            throw;
        }
    }

    public Task<IReadOnlyList<CommerceRecord>> ListAsync(string? processDate, CancellationToken cancellationToken)
    {
        DateOnly? date = string.IsNullOrWhiteSpace(processDate) ? null : ParseIsoDate(processDate);

        return repository.ListAsync(date, cancellationToken);
    }

    public Task<IReadOnlyList<QuarantineRecord>> ListQuarantineAsync(string? processDate, CancellationToken cancellationToken)
    {
        DateOnly? date = string.IsNullOrWhiteSpace(processDate) ? null : ParseIsoDate(processDate);

        return repository.ListQuarantineAsync(date, cancellationToken);
    }

    private static DateOnly ParseIsoDate(string value)
    {
        if (!ProcessDateParser.TryParseIso(value, out var date))
        {
            throw new RequestValidationException(
                "Solicitud inválida",
                "La fecha de proceso debe tener el formato yyyy-MM-dd y ser una fecha real.");
        }

        return date;
    }

    // The same status and message the API answers, so the audit shows what the user received
    private static (int Status, object Body) Describe(Exception exception) => exception switch
    {
        RequestValidationException e => (e.StatusCode, new { title = e.Title, detail = e.Message, errors = e.Errors }),
        ConflictException e => (409, new { title = "Registros existentes", detail = e.Message }),
        _ => (500, new { title = "Error interno" }),
    };
}
