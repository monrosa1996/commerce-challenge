namespace Commerce.Tests;

/// <summary>Reads the CSV scenarios from backend/database/samples.</summary>
internal static class Samples
{
    public static byte[] Read(string folder, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "database", "samples")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("Could not find backend/database/samples above the test output folder.");
        }

        return File.ReadAllBytes(Path.Combine(directory.FullName, "database", "samples", folder, fileName));
    }
}
