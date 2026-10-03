using Commerce.Application.Files;

namespace Commerce.Tests;

public sealed class FileNameValidatorTests
{
    [Theory]
    [InlineData("commerce_15032024.csv")]
    [InlineData("commerce_29022024.csv")]
    [InlineData("COMMERCE_15032024.CSV")]
    public void Accepts_valid_names(string name)
    {
        var valid = FileNameValidator.TryValidate(name, out var sourceFile, out var error);

        Assert.True(valid);
        Assert.Null(error);
        Assert.Equal(name, sourceFile);
    }

    [Theory]
    [InlineData("commerce_31022024.csv")]
    [InlineData("commerce_15132024.csv")]
    [InlineData("comercio_15032024.csv")]
    [InlineData("commerce_15032024.txt")]
    [InlineData("commerce_1503202.csv")]
    [InlineData("commerce_15032024.csv.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_names(string? name)
    {
        var valid = FileNameValidator.TryValidate(name, out _, out var error);

        Assert.False(valid);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Drops_the_client_folder()
    {
        var valid = FileNameValidator.TryValidate("/tmp/uploads/commerce_15032024.csv", out var sourceFile, out _);

        Assert.True(valid);
        Assert.Equal("commerce_15032024.csv", sourceFile);
    }
}
