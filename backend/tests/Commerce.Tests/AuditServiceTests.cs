using Commerce.Application.Audit;

namespace Commerce.Tests;

public sealed class AuditServiceTests
{
    [Fact]
    public async Task Payloads_are_saved_as_readable_json_with_accents()
    {
        var repository = new FakeAuditRepository();
        var service = new AuditService(repository);

        await service.RecordAsync(AuditActions.Process, new AuditContext(1, "admin", null), 400, new { processDate = "x" }, new { detail = "La fecha de proceso es obligatoria." });

        var entry = Assert.Single(repository.Entries);
        Assert.Equal("{\"processDate\":\"x\"}", entry.RequestPayload);
        Assert.Contains("obligatoria.", entry.ResponsePayload);
        Assert.DoesNotContain("\\u", entry.ResponsePayload);
    }

    [Fact]
    public async Task A_missing_payload_is_saved_as_null()
    {
        var repository = new FakeAuditRepository();

        await new AuditService(repository).RecordAsync(AuditActions.Logout, new AuditContext(1, "admin", null), 200, null, null);

        Assert.Null(repository.Entries[0].RequestPayload);
        Assert.Null(repository.Entries[0].ResponsePayload);
    }

    [Fact]
    public async Task Long_names_and_addresses_are_cut_to_the_size_of_the_columns()
    {
        var repository = new FakeAuditRepository();

        await new AuditService(repository).RecordAsync(
            AuditActions.LoginFailed, new AuditContext(null, new string('a', 200), new string('1', 100)), 401, null, null);

        Assert.Equal(50, repository.Entries[0].Username.Length);
        Assert.Equal(45, repository.Entries[0].IpAddress!.Length);
    }
}
