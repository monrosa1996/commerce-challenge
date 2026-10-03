using System.ComponentModel.DataAnnotations;

namespace Commerce.Application.Files;

public sealed class UploadOptions
{
    public const int MaxReportedErrors = 50;

    [Range(1, 50)]
    public int MaxFileSizeMb { get; set; } = 5;

    [Range(1, 1_000_000)]
    public int MaxRows { get; set; } = 50_000;

    public long MaxFileSizeBytes => MaxFileSizeMb * 1024L * 1024L;
}
