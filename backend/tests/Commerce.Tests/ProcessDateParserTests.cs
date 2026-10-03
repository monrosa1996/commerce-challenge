using Commerce.Application.Files;

namespace Commerce.Tests;

public sealed class ProcessDateParserTests
{
    [Fact]
    public void Parses_iso_format()
    {
        var parsed = ProcessDateParser.TryParse("2024-03-15", out var date, out var format);

        Assert.True(parsed);
        Assert.Equal(new DateOnly(2024, 3, 15), date);
        Assert.Equal(DateFormat.Iso, format);
    }

    [Fact]
    public void Parses_slash_format()
    {
        var parsed = ProcessDateParser.TryParse("15/03/2024", out var date, out var format);

        Assert.True(parsed);
        Assert.Equal(new DateOnly(2024, 3, 15), date);
        Assert.Equal(DateFormat.Slash, format);
    }

    [Theory]
    [InlineData("31/02/2024")]
    [InlineData("2024-02-30")]
    [InlineData("15-03-2024")]
    [InlineData("2024/03/15")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_dates(string? value)
    {
        Assert.False(ProcessDateParser.TryParse(value, out _, out _));
    }

    [Fact]
    public void Process_endpoint_accepts_only_iso()
    {
        Assert.True(ProcessDateParser.TryParseIso("2024-03-15", out _));
        Assert.False(ProcessDateParser.TryParseIso("15/03/2024", out _));
    }
}
