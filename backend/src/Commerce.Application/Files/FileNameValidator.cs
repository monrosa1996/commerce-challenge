using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Commerce.Application.Files;

public static partial class FileNameValidator
{
    [GeneratedRegex("^commerce_([0-9]{2})([0-9]{2})([0-9]{4})\\.csv$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    /// <summary>Checks commerce_DDMMYYYY.csv with a real date. The client path is never used.</summary>
    public static bool TryValidate(string? fileName, out string sourceFile, [NotNullWhen(false)] out string? error)
    {
        sourceFile = string.Empty;
        var name = Path.GetFileName(fileName ?? string.Empty);
        var match = NamePattern().Match(name);

        if (!match.Success)
        {
            error = "El nombre del archivo debe tener el formato commerce_DDMMYYYY.csv";
            return false;
        }

        var text = match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value;
        if (!DateOnly.TryParseExact(text, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            error = "El nombre del archivo contiene una fecha inválida";
            return false;
        }

        sourceFile = name;
        error = null;
        return true;
    }
}
