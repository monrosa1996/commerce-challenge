using System.Text;
using Commerce.Application.Errors;
using Commerce.Application.Files;

namespace Commerce.Tests;

public sealed class CsvFileValidatorTests
{
    private const string Header = "pc_processdate,pc_codcomred,pc_nomcomred,pc_tipdoc,pc_numdoc";

    private static CsvFileValidator Create(UploadOptions? options = null) => new(options ?? new UploadOptions());

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static RequestValidationException Fails(string fileName, byte[] content, UploadOptions? options = null) =>
        Assert.Throws<RequestValidationException>(() => Create(options).Validate(fileName, content));

    [Fact]
    public void Scenario_valid_comma_reads_ten_rows()
    {
        var result = Create().Validate("commerce_15032024.csv", Samples.Read("valid-comma", "commerce_15032024.csv"));

        Assert.Equal(10, result.Rows.Count);
        Assert.Equal(',', result.Separator);
        Assert.Empty(result.Warnings);
        Assert.Equal("1001", result.Rows[0].CodComRed);
        Assert.Equal(new DateOnly(2024, 3, 15), result.Rows[0].ProcessDate);
        Assert.Null(result.Rows[5].NomComRed);
        Assert.Null(result.Rows[6].NumDoc);
    }

    [Fact]
    public void Scenario_valid_semicolon_reads_ten_rows()
    {
        var result = Create().Validate("commerce_16032024.csv", Samples.Read("valid-semicolon", "commerce_16032024.csv"));

        Assert.Equal(10, result.Rows.Count);
        Assert.Equal(';', result.Separator);
        Assert.Equal(new DateOnly(2024, 3, 16), result.Rows[0].ProcessDate);
    }

    [Fact]
    public void Scenario_all_correct_has_ten_rows_and_no_warnings()
    {
        var result = Create().Validate("commerce_21032024.csv", Samples.Read("all-correct", "commerce_21032024.csv"));

        Assert.Equal(10, result.Rows.Count);
        Assert.Empty(result.Warnings);
        Assert.All(result.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.NomComRed)));
    }

    [Fact]
    public void Scenario_injection_attempt_is_read_as_plain_text()
    {
        var result = Create().Validate("commerce_22032024.csv", Samples.Read("injection-attempt", "commerce_22032024.csv"));

        Assert.Equal(5, result.Rows.Count);
        Assert.Equal("Robert'); DROP TABLE dbo.commerce;--", result.Rows[0].NomComRed);
        Assert.Equal("<script>alert('xss')</script>", result.Rows[2].NomComRed);
        Assert.Equal("0944';DROP TABLE x;--", result.Rows[3].NumDoc);
    }

    [Fact]
    public void Scenario_empty_is_rejected()
    {
        var error = Fails("commerce_17032024.csv", Samples.Read("empty", "commerce_17032024.csv"));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal("El archivo está vacío", error.Message);
    }

    [Fact]
    public void Scenario_missing_column_names_the_column()
    {
        var error = Fails("commerce_18032024.csv", Samples.Read("missing-column", "commerce_18032024.csv"));

        Assert.Equal(400, error.StatusCode);
        Assert.Contains("pc_numdoc", error.Message);
        Assert.Contains(error.Errors, e => e.Column == "pc_numdoc");
    }

    [Fact]
    public void Scenario_invalid_date_reports_row_seven()
    {
        var error = Fails("commerce_19032024.csv", Samples.Read("invalid-date", "commerce_19032024.csv"));

        var item = Assert.Single(error.Errors);
        Assert.Equal(7, item.Row);
        Assert.Equal("pc_processdate", item.Column);
        Assert.Contains("31/02/2024", item.Message);
    }

    [Fact]
    public void Scenario_duplicate_key_reports_rows_three_and_eight()
    {
        var error = Fails("commerce_20032024.csv", Samples.Read("duplicate-key", "commerce_20032024.csv"));

        Assert.Equal(new int?[] { 3, 8 }, error.Errors.Select(e => e.Row).ToArray());
    }

    [Fact]
    public void Rejects_file_with_only_header()
    {
        var error = Fails("commerce_15032024.csv", Bytes(Header + "\n"));

        Assert.Equal("El archivo no contiene registros", error.Message);
    }

    [Fact]
    public void Rejects_extra_column()
    {
        var error = Fails("commerce_15032024.csv", Bytes(Header + ",pc_extra\n2024-03-15,1001,Bazar,RUC,123,x\n"));

        Assert.Contains("pc_extra", error.Message);
    }

    [Fact]
    public void Rejects_repeated_column()
    {
        var error = Fails("commerce_15032024.csv", Bytes(Header + ",pc_numdoc\n2024-03-15,1001,Bazar,RUC,123,456\n"));

        Assert.Contains("repetidas", error.Message);
    }

    [Fact]
    public void Rejects_row_with_missing_field()
    {
        var content = Bytes(Header + "\n2024-03-15,1001,Bazar,RUC,123\n2024-03-15,1002,Farmacia,CED\n");

        var error = Fails("commerce_15032024.csv", content);

        var item = Assert.Single(error.Errors);
        Assert.Equal(2, item.Row);
    }

    [Fact]
    public void Rejects_empty_code()
    {
        var error = Fails("commerce_15032024.csv", Bytes(Header + "\n2024-03-15,,Bazar,RUC,123\n"));

        Assert.Contains(error.Errors, e => e.Row == 1 && e.Column == "pc_codcomred");
    }

    [Fact]
    public void Rejects_too_many_rows()
    {
        var text = Header + "\n2024-03-15,1,A,RUC,1\n2024-03-15,2,B,RUC,2\n2024-03-15,3,C,RUC,3\n";

        var error = Fails("commerce_15032024.csv", Bytes(text), new UploadOptions { MaxRows = 2 });

        Assert.Contains("2 filas", error.Message);
    }

    [Fact]
    public void Rejects_file_above_size_limit_with_413()
    {
        var content = new byte[(1024 * 1024) + 1];
        Array.Fill(content, (byte)'a');

        var error = Fails("commerce_15032024.csv", content, new UploadOptions { MaxFileSizeMb = 1 });

        Assert.Equal(413, error.StatusCode);
    }

    [Fact]
    public void Rejects_binary_content_renamed_as_csv()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

        var error = Fails("commerce_15032024.csv", png);

        Assert.Equal("El archivo no es un CSV de texto", error.Message);
    }

    [Fact]
    public void Rejects_bad_file_name()
    {
        var error = Fails("comercio_15032024.csv", Bytes(Header + "\n2024-03-15,1001,Bazar,RUC,123\n"));

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void Accepts_both_date_formats_with_a_warning()
    {
        var content = Bytes(Header + "\n2024-03-15,1001,Bazar,RUC,123\n16/03/2024,1002,Farmacia,CED,456\n");

        var result = Create().Validate("commerce_15032024.csv", content);

        Assert.Equal(2, result.Rows.Count);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Keeps_commas_inside_quotes()
    {
        var content = Bytes(Header + "\n2024-03-15,1001,\"Comercial, S.A.\",RUC,123\n");

        var result = Create().Validate("commerce_15032024.csv", content);

        Assert.Equal("Comercial, S.A.", Assert.Single(result.Rows).NomComRed);
    }

    [Fact]
    public void Accepts_utf8_bom()
    {
        var content = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Bytes(Header + "\n2024-03-15,1001,Bazar,RUC,123\n"))
            .ToArray();

        var result = Create().Validate("commerce_15032024.csv", content);

        Assert.Single(result.Rows);
    }

    [Fact]
    public void Accepts_windows_line_endings_and_header_in_any_order()
    {
        var content = Bytes("pc_numdoc,pc_tipdoc,pc_nomcomred,pc_codcomred,pc_processdate\r\n123,RUC,Bazar,1001,2024-03-15\r\n");

        var row = Assert.Single(Create().Validate("commerce_15032024.csv", content).Rows);

        Assert.Equal("1001", row.CodComRed);
        Assert.Equal("123", row.NumDoc);
        Assert.Equal("Bazar", row.NomComRed);
    }

    [Fact]
    public void Caps_reported_errors_at_fifty()
    {
        var builder = new StringBuilder(Header).Append('\n');
        for (var i = 0; i < 80; i++)
        {
            builder.Append("31/02/2024,").Append(i + 1).Append(",A,RUC,1\n");
        }

        var error = Fails("commerce_15032024.csv", Bytes(builder.ToString()));

        Assert.Equal(UploadOptions.MaxReportedErrors, error.Errors.Count);
    }
}
