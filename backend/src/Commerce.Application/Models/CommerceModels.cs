namespace Commerce.Application.Models;

public sealed record CommerceRow(
    DateOnly ProcessDate,
    string CodComRed,
    string? NomComRed,
    string? TipDoc,
    string? NumDoc);

public sealed record FileError(int? Row, string? Column, string Message);

public sealed record UploadResult(string SourceFile, int InsertedCount, IReadOnlyList<string> Warnings);

public sealed record ProcessRequest(string? ProcessDate);

public sealed record ProcessResult(DateOnly ProcessDate, int QuarantinedCount);

/// <summary>A record in the commerce table.</summary>
public sealed record CommerceRecord(
    long Id,
    string SourceFile,
    DateOnly ProcessDate,
    string CodComRed,
    string? NomComRed,
    string? TipDoc,
    string? NumDoc,
    DateTime CreatedAt);

public sealed record QuarantineRecord(
    long Id,
    string SourceFile,
    DateOnly ProcessDate,
    string CodComRed,
    string? NomComRed,
    string? TipDoc,
    string? NumDoc,
    string Motivo,
    DateTime QuarantinedAt);
