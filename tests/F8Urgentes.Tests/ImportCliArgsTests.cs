using F8Urgentes.Configuration;

namespace F8Urgentes.Tests;

public class ImportCliArgsTests
{
    [Fact]
    public void TryParseImportArgs_NoImportFlag_ReturnsNull()
    {
        Assert.Null(ImportCliArgs.TryParseImportArgs(["--smoke-test"]));
    }

    [Fact]
    public void TryParseImportArgs_ImportWithPath_ReturnsPathAndForceFalse()
    {
        var result = ImportCliArgs.TryParseImportArgs(["--import", "C:\\data\\file.xlsx"]);

        Assert.NotNull(result);
        Assert.Equal("C:\\data\\file.xlsx", result.Value.Path);
        Assert.False(result.Value.Force);
    }

    [Fact]
    public void TryParseImportArgs_ImportWithoutPath_ReturnsNullPath()
    {
        var result = ImportCliArgs.TryParseImportArgs(["--import"]);

        Assert.NotNull(result);
        Assert.Null(result.Value.Path);
    }

    [Fact]
    public void TryParseImportArgs_WithForce_ReturnsForceTrue()
    {
        var result = ImportCliArgs.TryParseImportArgs(["--import", "file.xlsx", "--force"]);

        Assert.NotNull(result);
        Assert.True(result.Value.Force);
    }
}
