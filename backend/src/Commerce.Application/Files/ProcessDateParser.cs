using System.Globalization;

namespace Commerce.Application.Files;

public enum DateFormat
{
    Iso,
    Slash,
}

public static class ProcessDateParser
{
    private const string IsoFormat = "yyyy-MM-dd";
    private const string SlashFormat = "dd/MM/yyyy";

    /// <summary>Only yyyy-MM-dd. Used by the process endpoint.</summary>
    public static bool TryParseIso(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value?.Trim(), IsoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>yyyy-MM-dd or dd/MM/yyyy. Used for the dates inside the CSV file.</summary>
    public static bool TryParse(string? value, out DateOnly date, out DateFormat format)
    {
        var text = value?.Trim();

        if (DateOnly.TryParseExact(text, IsoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            format = DateFormat.Iso;
            return true;
        }

        if (DateOnly.TryParseExact(text, SlashFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            format = DateFormat.Slash;
            return true;
        }

        format = default;
        return false;
    }
}
