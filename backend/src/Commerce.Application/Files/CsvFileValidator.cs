using System.Globalization;
using System.Text;
using Commerce.Application.Errors;
using Commerce.Application.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace Commerce.Application.Files;

public sealed record ValidatedFile(
    string SourceFile,
    char Separator,
    IReadOnlyList<CommerceRow> Rows,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Checks the file in memory, in this order: empty, name, size, text, separator, header,
/// no rows, too many rows, field count, date, code, length, repeated key.
/// Format errors reject the whole file because date and code form the key.
/// Business rules (name and number of the commerce) are checked later in SQL.
/// </summary>
public sealed class CsvFileValidator(UploadOptions options)
{
    private const string Title = "Archivo inválido";

    private static readonly string[] ExpectedColumns =
        ["pc_processdate", "pc_codcomred", "pc_nomcomred", "pc_tipdoc", "pc_numdoc"];

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly char[] LineBreaks = ['\r', '\n'];

    private const int MaxCodComRedLength = 20;
    private const int MaxNomComRedLength = 150;
    private const int MaxTipDocLength = 10;
    private const int MaxNumDocLength = 30;

    public ValidatedFile Validate(string? fileName, byte[]? content)
    {
        if (content is null || content.Length == 0)
        {
            throw Invalid("El archivo está vacío");
        }

        if (!FileNameValidator.TryValidate(fileName, out var sourceFile, out var nameError))
        {
            throw Invalid(nameError);
        }

        if (content.Length > options.MaxFileSizeBytes)
        {
            throw new RequestValidationException(
                "Archivo demasiado grande",
                $"El archivo supera el máximo de {options.MaxFileSizeMb} MB",
                StatusCodes.PayloadTooLarge);
        }

        var text = Decode(content);
        var separator = DetectSeparator(text);
        var records = ReadRecords(text, separator);

        if (records.Count == 0)
        {
            throw Invalid("El archivo no contiene registros");
        }

        var header = records[0].Select(h => h.ToLowerInvariant()).ToArray();
        var columns = ReadHeader(header);

        if (records.Count == 1)
        {
            throw Invalid("El archivo no contiene registros");
        }

        var rows = new List<CommerceRow>(records.Count - 1);
        var errors = ReadRows(records, header.Length, columns, rows, out var formats);

        if (errors.Count > 0)
        {
            throw Invalid(
                "El archivo tiene filas con errores de formato.",
                errors.OrderBy(e => e.Row ?? 0).Take(UploadOptions.MaxReportedErrors).ToList());
        }

        var warnings = new List<string>();
        if (formats.Count > 1)
        {
            warnings.Add("El archivo mezcla fechas con formato yyyy-MM-dd y dd/MM/yyyy. Se cargó igual.");
        }

        return new ValidatedFile(sourceFile, separator, rows, warnings);
    }

    private static string Decode(byte[] content)
    {
        if (content.AsSpan().IndexOf((byte)0) >= 0)
        {
            throw NotText();
        }

        try
        {
            return StrictUtf8.GetString(content).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            throw NotText();
        }
    }

    private static char DetectSeparator(string text)
    {
        var end = text.IndexOfAny(LineBreaks);
        var header = end < 0 ? text : text[..end];
        var commas = header.Count(c => c == ',');
        var semicolons = header.Count(c => c == ';');

        if (commas == 0 && semicolons == 0)
        {
            throw Invalid("No se pudo detectar el separador (',' o ';') en el encabezado");
        }

        return commas >= semicolons ? ',' : ';';
    }

    private List<string[]> ReadRecords(string text, char separator)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = separator.ToString(),
            HasHeaderRecord = false,
            Mode = CsvMode.RFC4180,
            BadDataFound = null,
        };

        var records = new List<string[]>();

        try
        {
            using var reader = new StringReader(text);
            using var parser = new CsvParser(reader, config);

            while (parser.Read())
            {
                var record = parser.Record;
                if (record is null || record.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                records.Add(record.Select(f => f.Trim()).ToArray());

                if (records.Count > options.MaxRows + 1)
                {
                    throw Invalid($"El archivo supera el máximo de {options.MaxRows} filas");
                }
            }
        }
        catch (CsvHelperException)
        {
            throw Invalid("El archivo CSV está mal formado. Revise las comillas.");
        }

        return records;
    }

    private static Dictionary<string, int> ReadHeader(string[] header)
    {
        var missing = ExpectedColumns.Where(c => !header.Contains(c)).ToList();
        var extra = header.Where(h => !ExpectedColumns.Contains(h)).Distinct().ToList();
        var repeated = header.GroupBy(h => h).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        if (missing.Count > 0 || extra.Count > 0 || repeated.Count > 0)
        {
            var parts = new List<string>();
            if (missing.Count > 0) parts.Add($"Faltan columnas: {string.Join(", ", missing)}.");
            if (extra.Count > 0) parts.Add($"Sobran columnas: {string.Join(", ", extra)}.");
            if (repeated.Count > 0) parts.Add($"Columnas repetidas: {string.Join(", ", repeated)}.");

            var errors = missing.Select(c => new FileError(null, c, "Columna faltante"))
                .Concat(extra.Select(c => new FileError(null, c, "Columna no esperada")))
                .Concat(repeated.Select(c => new FileError(null, c, "Columna repetida")))
                .ToList();

            throw Invalid($"El encabezado no tiene la estructura esperada. {string.Join(' ', parts)}", errors);
        }

        return ExpectedColumns.ToDictionary(c => c, c => Array.IndexOf(header, c));
    }

    private static List<FileError> ReadRows(
        List<string[]> records,
        int fieldCount,
        Dictionary<string, int> columns,
        List<CommerceRow> rows,
        out HashSet<DateFormat> formats)
    {
        var errors = new List<FileError>();
        var keyRows = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        formats = [];

        // records[0] is the header, so the data row number equals the index
        for (var number = 1; number < records.Count && errors.Count < UploadOptions.MaxReportedErrors; number++)
        {
            var fields = records[number];

            if (fields.Length != fieldCount)
            {
                errors.Add(new FileError(number, null, $"La fila tiene {fields.Length} campos y se esperaban {fieldCount}"));
                continue;
            }

            var dateText = fields[columns["pc_processdate"]];
            var code = fields[columns["pc_codcomred"]];
            var name = fields[columns["pc_nomcomred"]];
            var docType = fields[columns["pc_tipdoc"]];
            var docNumber = fields[columns["pc_numdoc"]];
            var valid = true;

            DateOnly date = default;
            if (ProcessDateParser.TryParse(dateText, out date, out var format))
            {
                formats.Add(format);
            }
            else
            {
                errors.Add(new FileError(number, "pc_processdate", $"Fecha inválida: '{dateText}'"));
                valid = false;
            }

            if (code.Length == 0)
            {
                errors.Add(new FileError(number, "pc_codcomred", "El código del comercio está vacío"));
                valid = false;
            }

            valid &= CheckLength(errors, number, "pc_codcomred", code, MaxCodComRedLength);
            valid &= CheckLength(errors, number, "pc_nomcomred", name, MaxNomComRedLength);
            valid &= CheckLength(errors, number, "pc_tipdoc", docType, MaxTipDocLength);
            valid &= CheckLength(errors, number, "pc_numdoc", docNumber, MaxNumDocLength);

            if (!valid)
            {
                continue;
            }

            rows.Add(new CommerceRow(date, code, NullIfEmpty(name), NullIfEmpty(docType), NullIfEmpty(docNumber)));

            var key = KeyOf(date, code);
            if (!keyRows.TryGetValue(key, out var list))
            {
                list = [];
                keyRows[key] = list;
            }

            list.Add(number);
        }

        foreach (var (key, list) in keyRows.Where(pair => pair.Value.Count > 1))
        {
            foreach (var number in list)
            {
                errors.Add(new FileError(
                    number,
                    "pc_codcomred",
                    $"Clave repetida {key} en las filas {string.Join(", ", list)}"));
            }
        }

        return errors;
    }

    private static bool CheckLength(List<FileError> errors, int row, string column, string value, int max)
    {
        if (value.Length <= max)
        {
            return true;
        }

        errors.Add(new FileError(row, column, $"El valor supera los {max} caracteres"));
        return false;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string KeyOf(DateOnly date, string code) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + code;

    private static RequestValidationException NotText() => Invalid("El archivo no es un CSV de texto");

    private static RequestValidationException Invalid(string detail, IReadOnlyList<FileError>? errors = null) =>
        new(Title, detail, StatusCodes.BadRequest, errors);

    private static class StatusCodes
    {
        public const int BadRequest = 400;
        public const int PayloadTooLarge = 413;
    }
}
