using Commerce.Application.Audit;
using Commerce.Application.Errors;
using Commerce.Application.Files;
using Commerce.Application.Services;

namespace Commerce.Tests;

public sealed class CommerceServiceTests
{
    private static readonly AuditContext User = new(7, "analista", "10.0.0.5");

    private static (CommerceService Service, FakeCommerceRepository Repository, FakeAuditRepository Audit) Create()
    {
        var repository = new FakeCommerceRepository();
        var audit = new FakeAuditRepository();
        var service = new CommerceService(repository, new CsvFileValidator(new UploadOptions()), new AuditService(audit));

        return (service, repository, audit);
    }

    [Fact]
    public async Task Upload_valid_file_sends_all_rows_once()
    {
        var (service, repository, _) = Create();
        var content = Samples.Read("valid-comma", "commerce_15032024.csv");

        var result = await service.UploadAsync("commerce_15032024.csv", content, User, CancellationToken.None);

        Assert.Equal(10, result.InsertedCount);
        Assert.Equal("commerce_15032024.csv", result.SourceFile);
        Assert.Equal(1, repository.CreateCalls);
        Assert.Equal(10, repository.CreatedRows.Count);
    }

    [Fact]
    public async Task Upload_invalid_file_never_reaches_the_repository()
    {
        var (service, repository, _) = Create();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => service.UploadAsync("commerce_15032024.csv", [], User, CancellationToken.None));

        Assert.Equal(0, repository.CreateCalls);
    }

    [Fact]
    public async Task Upload_is_audited_with_what_came_in_and_what_went_out()
    {
        var (service, _, audit) = Create();
        var content = Samples.Read("valid-comma", "commerce_15032024.csv");

        await service.UploadAsync("commerce_15032024.csv", content, User, CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.Upload, entry.Action);
        Assert.Equal(201, entry.StatusCode);
        Assert.Equal(7, entry.UserId);
        Assert.Equal("analista", entry.Username);
        Assert.Equal("10.0.0.5", entry.IpAddress);
        Assert.Contains("\"fileName\":\"commerce_15032024.csv\"", entry.RequestPayload);
        Assert.Contains($"\"sizeBytes\":{content.Length}", entry.RequestPayload);
        Assert.Contains("\"sha256\":\"", entry.RequestPayload);
        Assert.Contains("\"insertedCount\":10", entry.ResponsePayload);
    }

    [Fact]
    public async Task Upload_does_not_save_the_content_of_the_file_in_the_audit()
    {
        var (service, _, audit) = Create();
        var content = Samples.Read("valid-comma", "commerce_15032024.csv");

        await service.UploadAsync("commerce_15032024.csv", content, User, CancellationToken.None);

        Assert.DoesNotContain("Ferreteria", audit.Entries[0].RequestPayload);
    }

    [Fact]
    public async Task Upload_rejected_by_the_file_rules_is_audited_with_the_message_the_user_got()
    {
        var (service, _, audit) = Create();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => service.UploadAsync("commerce_15032024.csv", [], User, CancellationToken.None));

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(400, entry.StatusCode);
        Assert.Contains("\"sizeBytes\":0", entry.RequestPayload);
        Assert.Contains("El archivo está vacío", entry.ResponsePayload);
    }

    [Fact]
    public async Task Upload_of_existing_keys_is_audited_as_409()
    {
        var (service, repository, audit) = Create();
        repository.Failure = new ConflictException("Hay registros que ya existen en el sistema");
        var content = Samples.Read("valid-comma", "commerce_15032024.csv");

        await Assert.ThrowsAsync<ConflictException>(
            () => service.UploadAsync("commerce_15032024.csv", content, User, CancellationToken.None));

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(409, entry.StatusCode);
        Assert.Contains("Hay registros que ya existen", entry.ResponsePayload);
    }

    [Fact]
    public async Task Upload_with_an_unexpected_failure_is_audited_as_500_without_internal_details()
    {
        var (service, repository, audit) = Create();
        repository.Failure = new InvalidOperationException("connection string with a secret");
        var content = Samples.Read("valid-comma", "commerce_15032024.csv");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UploadAsync("commerce_15032024.csv", content, User, CancellationToken.None));

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(500, entry.StatusCode);
        Assert.DoesNotContain("secret", entry.ResponsePayload);
    }

    [Fact]
    public async Task Process_returns_the_quarantined_count_and_is_audited()
    {
        var (service, repository, audit) = Create();

        var result = await service.ProcessAsync("2024-03-15", User, CancellationToken.None);

        Assert.Equal(5, result.QuarantinedCount);
        Assert.Equal(new DateOnly(2024, 3, 15), result.ProcessDate);
        Assert.Equal(new DateOnly(2024, 3, 15), repository.ProcessedDate);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.Process, entry.Action);
        Assert.Equal(200, entry.StatusCode);
        Assert.Contains("\"processDate\":\"2024-03-15\"", entry.RequestPayload);
        Assert.Contains("\"quarantinedCount\":5", entry.ResponsePayload);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("15/03/2024")]
    [InlineData("2024-02-30")]
    public async Task Process_rejects_missing_or_bad_date_and_audits_it(string? value)
    {
        var (service, repository, audit) = Create();

        var error = await Assert.ThrowsAsync<RequestValidationException>(
            () => service.ProcessAsync(value, User, CancellationToken.None));

        Assert.Equal(400, error.StatusCode);
        Assert.Null(repository.ProcessedDate);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.Process, entry.Action);
        Assert.Equal(400, entry.StatusCode);
    }

    [Fact]
    public async Task List_quarantine_without_date_has_no_filter_and_is_not_audited()
    {
        var (service, repository, audit) = Create();

        await service.ListQuarantineAsync(null, CancellationToken.None);

        Assert.Null(repository.ProcessedDate);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task List_passes_the_date_filter_to_the_repository()
    {
        var (service, repository, _) = Create();

        await service.ListAsync("2024-03-15", CancellationToken.None);

        Assert.Equal(new DateOnly(2024, 3, 15), repository.ProcessedDate);
    }

    [Fact]
    public async Task List_rejects_a_bad_date()
    {
        var (service, _, _) = Create();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => service.ListAsync("15/03/2024", CancellationToken.None));
    }
}
